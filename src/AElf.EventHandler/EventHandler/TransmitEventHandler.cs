using System;
using System.Threading.Tasks;
using AElf.Client.Core;
using AElf.Client.Core.Options;
using AElf.EventHandler.Dto;
using AElf.Nethereum.Bridge;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace AElf.EventHandler.EventHandler;

public class TransmitEventHandler :
    IDistributedEventHandler<TransmitEto>, ISingletonDependency
{
    private readonly IAElfClientService _aelfClientService;
    private readonly AElfChainAliasOptions _aelfChainAliasOption;
    private readonly RetryTransmitInfoOptions _retryTransmitInfoOptions;
    private readonly IBridgeOutService _bridgeOutService;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IObjectMapper<EventHandlerAppModule> _objectMapper;
    private readonly ITransmitTransactionProvider _transmitTransactionProvider;

    public ILogger<TransmitEventHandler> Logger { get; set; }

    public TransmitEventHandler(
        IAElfClientService aelfClientService,
        IOptionsSnapshot<AElfChainAliasOptions> aelfChainAliasOption,
        IOptionsSnapshot<RetryTransmitInfoOptions> retryTransmitInfoOptions,
        IBridgeOutService bridgeOutService,
        IDistributedEventBus distributedEventBus,
        IDistributedCacheSerializer serializer,
        IObjectMapper<EventHandlerAppModule> objectMapper,
        ITransmitTransactionProvider transmitTransactionProvider)
    {
        _aelfClientService = aelfClientService;
        _bridgeOutService = bridgeOutService;
        _distributedEventBus = distributedEventBus;
        _objectMapper = objectMapper;
        _transmitTransactionProvider = transmitTransactionProvider;
        _aelfChainAliasOption = aelfChainAliasOption.Value;
        _retryTransmitInfoOptions = retryTransmitInfoOptions.Value;
    }

    public async Task HandleEventAsync(TransmitEto eventData)
    {
        long lib = 100;
        if (eventData.BlockHeight > lib)
        {
            throw new AbpException("Current transaction block height is higher than lib.");
        }

        if (eventData.LastSendTransmitTime != null)
        {
            if (!eventData.LastSendTransmitTime.HasValue ||
                eventData.LastSendTransmitTime.Value.AddMinutes(_retryTransmitInfoOptions.RetryTransmitTimePeriod) >
                DateTime.UtcNow)
            {
                throw new AbpException("Insufficient time interval since the last transmit was sent.");
            }
        }

        var block = await _aelfClientService.GetBlockByHeightAsync(_aelfChainAliasOption.Mapping[eventData.ChainId],
            eventData.BlockHeight);
        if (block?.BlockHash == eventData.BlockHash)
        {
            if (eventData.SendTimes > _retryTransmitInfoOptions.MaxSendTransmitTimes)
            {
                //redis
                eventData.SendTimes = 0;
                eventData.Time = DateTime.UtcNow;
                eventData.LastSendTransmitTime = null;
                await _transmitTransactionProvider.PushFailedTransmitAsync(eventData);
            }
            else
            {
                eventData.SendTimes += 1;
                try
                {
                    var sendResult = await _bridgeOutService.TransmitAsync(eventData.TargetChainId,
                        eventData.TargetContractAddress, eventData.SwapHashId, eventData.Report, eventData.Rs,
                        eventData.Ss,
                        eventData.RawVs);
                    if (string.IsNullOrWhiteSpace(sendResult))
                    {
                        Logger.LogError("Failed to transmit.");
                        eventData.LastSendTransmitTime = DateTime.UtcNow;
                        await _distributedEventBus.PublishAsync(eventData);
                    }
                    else
                    {
                        eventData.TransactionId = sendResult;
                        await _distributedEventBus.PublishAsync(
                            _objectMapper.Map<TransmitEto, TransmitCheckEto>(eventData));
                        Logger.LogInformation(
                            $"Send Transmit transaction. TxId: {sendResult}, Report: {eventData.Report.ToHex()}");
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Send Transmit transaction Failed. Message: {e.Message}", e);
                    eventData.LastSendTransmitTime = DateTime.UtcNow;
                    await _distributedEventBus.PublishAsync(eventData);
                }
            }
        }
    }
}