using System.Threading.Tasks;
using AElf.EventHandler.BackgroundJob;
using AElf.EventHandler.Dto;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public interface ITransmitTransactionProvider
{
    Task PushFailedTransmitAsync<T>(T data, string queue);
    Task ReSendFailedJobAsync();
    // Task ReSendExceedDailyJobAsync();
}

public class TransmitTransactionProvider : AbpRedisCache, ITransmitTransactionProvider, ISingletonDependency
{
    private readonly IDistributedCacheSerializer _serializer;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public ILogger<TransmitTransactionProvider> Logger { get; set; }


    public TransmitTransactionProvider(IOptions<RedisCacheOptions> optionsAccessor,
        IDistributedCacheSerializer serializer,
        IBackgroundJobManager backgroundJobManager)
        : base(optionsAccessor)
    {
        _serializer = serializer;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task PushFailedTransmitAsync<T>(T data, string queue)
    {
        await ConnectAsync();
        await RedisDatabase.ListRightPushAsync(queue,
            _serializer.Serialize(data));
    }

    public async Task ReSendFailedJobAsync()
    {
        Logger.LogInformation(
            "Start to resend failed transmit.");
        await ConnectAsync();
        await EnqueueRedis(QueueConstants.TransmitFailedList,BackgroundJobPriority.BelowNormal);
    }

    //Todo：Continue monitoring error message retrieval for subsequent upgrades.
    // public async Task ReSendExceedDailyJobAsync()
    // {
    //     Logger.LogInformation(
    //         "Start to resend exceed daily limit transmit.");
    //     await ConnectAsync();
    //     await EnqueueRedis(QueueConstants.ExceedDailyLimitList);
    // }

    private async Task EnqueueRedis(string queueName, BackgroundJobPriority priority = BackgroundJobPriority.Normal)
    {
        var list = await RedisDatabase.ListRangeAsync(queueName);
        if (list == null || list.Length == 0)
        {
            Logger.LogInformation(
                "No transmit to resend.");
            return;
        } 

        foreach (var item in list)
        {
            var toPublish = _serializer.Deserialize<TransmitArgs>(item);
            Logger.LogInformation(
                "Start to publish.chain id:{Item},swap id:{Id}", toPublish.ChainId, toPublish.SwapHashId);
            await _backgroundJobManager.EnqueueAsync(toPublish, priority);
            await RedisDatabase.ListLeftPopAsync((RedisKey)queueName);
        }
    }
}