using System;
using System.Threading.Tasks;
using AElf.EventHandler.Dto;
using AElf.Nethereum.Core;
using AElf.Nethereum.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace AElf.EventHandler.EventHandler;

public class TransmitCheckEventHandler :
    IDistributedEventHandler<TransmitCheckEto>, ISingletonDependency
{
    private readonly EthereumChainAliasOptions _ethereumAElfChainAliasOptions;
    private readonly INethereumService _nethereumService;
    private readonly BlockConfirmationOptions _blockConfirmationOptions;
    private readonly RetryTransmitInfoOptions _retryTransmitInfoOptions;
    public ILogger<TransmitCheckEventHandler> Logger { get; set; }
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ITransmitTransactionProvider _transmitTransactionProvider;
    private readonly BridgeOptions _bridgeOptions;



    public TransmitCheckEventHandler(
        IOptionsSnapshot<EthereumChainAliasOptions> ethereumAElfChainAliasOptions,
        INethereumService nethereumService,
        IOptionsSnapshot<BlockConfirmationOptions> blockConfirmationOptions,
        IOptionsSnapshot<RetryTransmitInfoOptions> retryTransmitInfoOptions,
        IOptionsSnapshot<BridgeOptions> bridgeOptions,
        IDistributedEventBus distributedEventBus,
        ITransmitTransactionProvider transmitTransactionProvider)
    {
        _nethereumService = nethereumService;
        _distributedEventBus = distributedEventBus;
        _transmitTransactionProvider = transmitTransactionProvider;
        _ethereumAElfChainAliasOptions = ethereumAElfChainAliasOptions.Value;
        _blockConfirmationOptions = blockConfirmationOptions.Value;
        _retryTransmitInfoOptions = retryTransmitInfoOptions.Value;
        _bridgeOptions = bridgeOptions.Value;
    }

    public async Task HandleEventAsync(TransmitCheckEto eventData)
    {
        if (!_bridgeOptions.IsTransmitter) return;
        if (eventData.LastSendTransmitCheckTime != null)
        {
            if (!eventData.LastSendTransmitCheckTime.HasValue ||
                eventData.LastSendTransmitCheckTime.Value.AddMinutes(_retryTransmitInfoOptions
                    .RetryTransmitCheckTimePeriod) > DateTime.UtcNow)
            {
                throw new AbpException(
                    $"Insufficient time interval.ChainId:{eventData.ChainId},Target Chain: {eventData.TargetChainId},swapId:{eventData.SwapHashId.ToHex()}");
            }
        }

        var ethAlias = _ethereumAElfChainAliasOptions.Mapping[eventData.TargetChainId];
        if (eventData.QueryTimes > _retryTransmitInfoOptions.MaxQueryTransmitTimes)
        {
            Logger.LogDebug(
                "Transmit transaction query failed after retry {Count} times. Chain id:{FromId},Target Chain: {Id}, TxId: {TxId}",
                _retryTransmitInfoOptions.MaxQueryTransmitTimes, eventData.ChainId, eventData.TargetChainId,
                eventData.TransactionId);
            PushFailedTransmitAsync(eventData);
        }
        else
        {
            eventData.QueryTimes += 1;
            try
            {
                var receipt = await _nethereumService.GetTransactionReceiptAsync(ethAlias, eventData.TransactionId);
                if (receipt == null || receipt.Status == null || receipt.Status.Value != 1)
                {
                    Logger.LogDebug(
                        "Transmit transaction query failed. Chain: {Id}, Target Chain: {TargetId}, TxId: {TxId}",
                        eventData.ChainId, eventData.TargetChainId, eventData.TransactionId);
                    eventData.LastSendTransmitCheckTime = DateTime.UtcNow;
                    await _distributedEventBus.PublishAsync(eventData);
                }
                else
                {
                    var currentHeight = await _nethereumService.GetBlockNumberAsync(ethAlias);
                    if (receipt.BlockNumber.ToLong() >=
                        currentHeight - _blockConfirmationOptions.ConfirmationCount[eventData.TargetChainId])
                    {
                        throw new AbpException(
                            $"Block is not confirmed.FromChainId:{eventData.ChainId},TargetChainId:{eventData.TargetChainId},SwapId:{eventData.SwapHashId.ToHex()},CurrentHeight:{currentHeight},BlockNumber:{receipt.BlockNumber}");
                    }

                    var block = await _nethereumService.GetBlockByNumberAsync(ethAlias, receipt.BlockNumber);
                    if (block.BlockHash != receipt.BlockHash)
                    {
                        Logger.LogError(
                            "Transmit transaction forked.From chain:{FromId},Target Chain: {Id},TxId: {TxId}",
                            eventData.ChainId, eventData.TargetChainId,
                            eventData.TransactionId);
                        PushFailedTransmitAsync(eventData);
                    }
                    else
                    {
                        Logger.LogInformation("Transmit transaction finished. TxId: {Id}", eventData.TransactionId);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(
                    "Send Transmit check transaction Failed,From chain:{FromId},Target Chain: {ChainId},Swap id:{SwapId},TxId:{Id}. Message: {Message}",
                    eventData.ChainId, eventData.TargetChainId, eventData.SwapHashId.ToHex(), eventData.TransactionId, e);
                eventData.LastSendTransmitCheckTime = DateTime.UtcNow;
                await _distributedEventBus.PublishAsync(eventData);
            }
        }
    }

    private async void PushFailedTransmitAsync(TransmitCheckEto eventData)
    {
        eventData.SendTimes = 0;
        eventData.Time = DateTime.UtcNow;
        await _transmitTransactionProvider.PushFailedTransmitAsync(eventData);
    }
}