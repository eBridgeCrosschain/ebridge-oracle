using System;
using System.Threading.Tasks;
using AElf.EventHandler.Dto;
using AElf.Nethereum.Core;
using AElf.Nethereum.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler.BackgroundJob;

public class TransmitCheckJob :
    AsyncBackgroundJob<TransmitCheckArgs>, ISingletonDependency
{
    private readonly EthereumChainAliasOptions _ethereumAElfChainAliasOptions;
    private readonly INethereumService _nethereumService;
    private readonly BlockConfirmationOptions _blockConfirmationOptions;
    private readonly RetryTransmitInfoOptions _retryTransmitInfoOptions;
    private readonly IBackgroundJobManager _backgroundJobManager;
    public ILogger<TransmitCheckJob> Logger { get; set; }
    private readonly ITransmitTransactionProvider _transmitTransactionProvider;
    private readonly BridgeOptions _bridgeOptions;

    public TransmitCheckJob(
        IOptionsSnapshot<EthereumChainAliasOptions> ethereumAElfChainAliasOptions,
        INethereumService nethereumService,
        IOptionsSnapshot<BlockConfirmationOptions> blockConfirmationOptions,
        IOptionsSnapshot<RetryTransmitInfoOptions> retryTransmitInfoOptions,
        IOptionsSnapshot<BridgeOptions> bridgeOptions,
        ITransmitTransactionProvider transmitTransactionProvider,
        IBackgroundJobManager backgroundJobManager)
    {
        _nethereumService = nethereumService;
        _transmitTransactionProvider = transmitTransactionProvider;
        _backgroundJobManager = backgroundJobManager;
        _ethereumAElfChainAliasOptions = ethereumAElfChainAliasOptions.Value;
        _blockConfirmationOptions = blockConfirmationOptions.Value;
        _retryTransmitInfoOptions = retryTransmitInfoOptions.Value;
        _bridgeOptions = bridgeOptions.Value;
    }

    public override async Task ExecuteAsync(TransmitCheckArgs args)
    {
        if (!_bridgeOptions.IsTransmitter) return;

        var ethAlias = _ethereumAElfChainAliasOptions.Mapping[args.TargetChainId];
        if (args.QueryTimes > _retryTransmitInfoOptions.MaxQueryTransmitTimes)
        {
            Logger.LogDebug(
                "Transmit transaction query failed after retry {Count} times. Chain id:{FromId},Target Chain: {Id}, TxId: {TxId}",
                _retryTransmitInfoOptions.MaxQueryTransmitTimes, args.ChainId, args.TargetChainId, args.TransactionId);
            PushFailedTransmitAsync(args);
        }
        else
        {
            args.QueryTimes += 1;
            try
            {
                var receipt = await _nethereumService.GetTransactionReceiptAsync(ethAlias, args.TransactionId);
                if (receipt == null || receipt.Status == null || receipt.Status.Value != 1)
                {
                    Logger.LogDebug(
                        "Transmit transaction query failed. Chain: {Id}, Target Chain: {TargetId}, TxId: {TxId}",
                        args.ChainId, args.TargetChainId, args.TransactionId);
                    await _backgroundJobManager.EnqueueAsync(args,
                        delay: TimeSpan.FromMinutes(_retryTransmitInfoOptions.RetryTransmitCheckTimePeriod));
                }
                else
                {
                    var currentHeight = await _nethereumService.GetBlockNumberAsync(ethAlias);
                    if (receipt.BlockNumber.ToLong() >=
                        currentHeight - _blockConfirmationOptions.ConfirmationCount[args.TargetChainId])
                    {
                        Logger.LogDebug(
                            "Block is not confirmed.FromChainId:{Id},TargetChainId:{TargetId},SwapId:{SwapId},CurrentHeight:{CurrentHeight},BlockNumber:{BlockNumber}",args.ChainId,args.TargetChainId,args.SwapId,currentHeight,receipt.BlockNumber);
                        await _backgroundJobManager.EnqueueAsync(args,
                            delay: TimeSpan.FromSeconds(_retryTransmitInfoOptions.RetryCheckLib));
                    }
                    else
                    {
                        var block = await _nethereumService.GetBlockByNumberAsync(ethAlias, receipt.BlockNumber);
                        if (block.BlockHash != receipt.BlockHash)
                        {
                            Logger.LogError(
                                "Transmit transaction forked.From chain:{FromId},Target Chain: {Id},TxId: {TxId}",
                                args.ChainId, args.TargetChainId, args.TransactionId);
                            PushFailedTransmitAsync(args);
                        }
                        else
                        {
                            Logger.LogInformation("Transmit transaction finished. TxId: {Id}", args.TransactionId);
                        }   
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(
                    "Send transmit check transaction Failed,From chain:{FromId},Target Chain: {ChainId},Swap id:{SwapId},TxId:{Id}. Message: {Message}",
                    args.ChainId, args.TargetChainId, args.SwapId, args.TransactionId, e);
                await _backgroundJobManager.EnqueueAsync(args,
                    delay: TimeSpan.FromMinutes(_retryTransmitInfoOptions.RetryTransmitCheckTimePeriod));
            }
        }
    }

    private async void PushFailedTransmitAsync(TransmitCheckArgs eventData)
    {
        eventData.Time = DateTime.UtcNow;
        await _transmitTransactionProvider.PushFailedTransmitAsync(eventData);
    }
}