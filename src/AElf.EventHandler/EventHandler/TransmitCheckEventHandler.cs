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


    public TransmitCheckEventHandler(
        IOptionsSnapshot<EthereumChainAliasOptions> ethereumAElfChainAliasOptions,
        INethereumService nethereumService,
        IOptionsSnapshot<BlockConfirmationOptions> blockConfirmationOptions,
        IOptionsSnapshot<RetryTransmitInfoOptions> retryTransmitInfoOptions,
        IDistributedEventBus distributedEventBus,
        ITransmitTransactionProvider transmitTransactionProvider)
    {
        _nethereumService = nethereumService;
        _distributedEventBus = distributedEventBus;
        _transmitTransactionProvider = transmitTransactionProvider;
        _ethereumAElfChainAliasOptions = ethereumAElfChainAliasOptions.Value;
        _blockConfirmationOptions = blockConfirmationOptions.Value;
        _retryTransmitInfoOptions = retryTransmitInfoOptions.Value;
    }

    public async Task HandleEventAsync(TransmitCheckEto eventData)
    {
        if (eventData.LastSendTransmitCheckTime != null)
        {
            if (!eventData.LastSendTransmitCheckTime.HasValue ||
                eventData.LastSendTransmitCheckTime.Value.AddMinutes(_retryTransmitInfoOptions
                    .RetryTransmitCheckTimePeriod) > DateTime.UtcNow)
            {
                throw new AbpException("Insufficient time interval.");
            }
        }

        var ethAlias = _ethereumAElfChainAliasOptions.Mapping[eventData.TargetChainId];
        if (eventData.QueryTimes > _retryTransmitInfoOptions.MaxQueryTransmitTimes)
        {
            Logger.LogDebug(
                $"Transmit transaction query failed after retry {_retryTransmitInfoOptions.MaxQueryTransmitTimes} times. Chain: {eventData.TargetChainId},  TxId: {eventData.TransactionId}");
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
                        $"Transmit transaction query failed. Chain: {eventData.TargetChainId},  TxId: {eventData.TransactionId}");
                    eventData.LastSendTransmitCheckTime = DateTime.UtcNow;
                    await _distributedEventBus.PublishAsync(eventData);
                }
                else
                {
                    var currentHeight = await _nethereumService.GetBlockNumberAsync(ethAlias);
                    if (receipt.BlockNumber.ToLong() >=
                        currentHeight - _blockConfirmationOptions.ConfirmationCount[eventData.TargetChainId])
                    {
                        throw new AbpException("Block is not confirmed.");
                    }

                    var block = await _nethereumService.GetBlockByNumberAsync(ethAlias, receipt.BlockNumber);
                    if (block.BlockHash != receipt.BlockHash)
                    {
                        Logger.LogError(
                            $"Transmit transaction forked. Chain: {eventData.TargetChainId},  TxId: {eventData.TransactionId}");
                        PushFailedTransmitAsync(eventData);
                    }
                    else
                    {
                        Logger.LogInformation($"Transmit transaction finished. TxId: {eventData.TransactionId}");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Send Transmit check transaction Failed. Message: {e.Message}", e);
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