using System;
using System.Threading.Tasks;
using AElf.EventHandler.Dto;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Hex.HexConvertors.Extensions;
using StackExchange.Redis;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace AElf.EventHandler;

public interface ITransmitTransactionProvider
{
    Task PushFailedTransmitAsync<T>(T data);
    Task ReSendFailedJobAsync();
}

public class TransmitTransactionProvider : AbpRedisCache, ITransmitTransactionProvider, ISingletonDependency
{
    private readonly IDistributedCacheSerializer _serializer;
    private readonly IBackgroundJobManager _backgroundJobManager;


    public ILogger<TransmitTransactionProvider> Logger { get; set; }

    private const string TransmitFailedList = "TransmitFailedList";

    public TransmitTransactionProvider(IOptions<RedisCacheOptions> optionsAccessor,
        IDistributedCacheSerializer serializer,
        IBackgroundJobManager backgroundJobManager)
        : base(optionsAccessor)
    {
        _serializer = serializer;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task PushFailedTransmitAsync<T>(T data)
    {
        await ConnectAsync();
        await RedisDatabase.ListRightPushAsync(TransmitFailedList,
            _serializer.Serialize(data));
    }

    public async Task ReSendFailedJobAsync()
    {
        Logger.LogInformation(
            $"Start to resend failed transmit.");
        await ConnectAsync();
        var list = await RedisDatabase.ListRangeAsync(TransmitFailedList);
        if (list == null || list.Length == 0)
        {
            Logger.LogInformation(
                $"No failed transmit to resend.");
            return;
        }

        foreach (var item in list)
        {
            var toPublish = _serializer.Deserialize<TransmitArgs>(item);
            Logger.LogInformation(
                "Start to publish.chain id:{Item},swap id:{Id}", toPublish.ChainId, toPublish.SwapHashId);
            await _backgroundJobManager.EnqueueAsync(toPublish, BackgroundJobPriority.BelowNormal);
            await RedisDatabase.ListLeftPopAsync((RedisKey) TransmitFailedList);
        }
    }
}