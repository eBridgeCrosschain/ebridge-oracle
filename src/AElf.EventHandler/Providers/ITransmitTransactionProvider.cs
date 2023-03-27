using System.Threading.Tasks;
using AElf.EventHandler.Dto;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
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
    private readonly IDistributedEventBus _distributedEventBus;

    public ILogger<TransmitTransactionProvider> Logger { get; set; }

    private const string TransmitFailedList = "TransmitFailedList";

    public TransmitTransactionProvider(IOptions<RedisCacheOptions> optionsAccessor,
        IDistributedCacheSerializer serializer,
        IDistributedEventBus distributedEventBus)
        : base(optionsAccessor)
    {
        _serializer = serializer;
        _distributedEventBus = distributedEventBus;
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
            await _distributedEventBus.PublishAsync(
                _serializer.Deserialize<TransmitEto>(item));
            await RedisDatabase.ListLeftPopAsync((RedisKey) TransmitFailedList);
        }
    }
}