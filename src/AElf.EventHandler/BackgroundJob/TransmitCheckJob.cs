using System;
using System.Threading.Tasks;
using AElf.EventHandler.Dto;
using AElf.Nethereum.Core;
using AElf.Nethereum.Core.Dtos;
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
    private readonly TronChainAliasOptions _tronChainAliasOptions;
    private readonly IBlockchainService _blockchainService;
    private readonly BlockConfirmationOptions _blockConfirmationOptions;
    private readonly RetryTransmitInfoOptions _retryTransmitInfoOptions;
    private readonly IBackgroundJobManager _backgroundJobManager;
    public ILogger<TransmitCheckJob> Logger { get; set; }
    private readonly ITransmitTransactionProvider _transmitTransactionProvider;
    private readonly BridgeOptions _bridgeOptions;

    public TransmitCheckJob(
        IOptionsSnapshot<EthereumChainAliasOptions> ethereumAElfChainAliasOptions,
        IOptionsSnapshot<TronChainAliasOptions> tronAElfChainAliasOptions,
        IBlockchainService blockchainService,
        IOptionsSnapshot<BlockConfirmationOptions> blockConfirmationOptions,
        IOptionsSnapshot<RetryTransmitInfoOptions> retryTransmitInfoOptions,
        IOptionsSnapshot<BridgeOptions> bridgeOptions,
        ITransmitTransactionProvider transmitTransactionProvider,
        IBackgroundJobManager backgroundJobManager)
    {
        _blockchainService = blockchainService;
        _transmitTransactionProvider = transmitTransactionProvider;
        _backgroundJobManager = backgroundJobManager;
        _ethereumAElfChainAliasOptions = ethereumAElfChainAliasOptions.Value;
        _tronChainAliasOptions = tronAElfChainAliasOptions.Value;
        _blockConfirmationOptions = blockConfirmationOptions.Value;
        _retryTransmitInfoOptions = retryTransmitInfoOptions.Value;
        _bridgeOptions = bridgeOptions.Value;
    }

    public override async Task ExecuteAsync(TransmitCheckArgs args)
    {
        if (!_bridgeOptions.IsTransmitter) return;

        if(!_ethereumAElfChainAliasOptions.Mapping.TryGetValue(args.TargetChainId, out var chainAlias))
        {
            chainAlias = _tronChainAliasOptions.Mapping[args.TargetChainId];
        }
        if (args.QueryTimes > _retryTransmitInfoOptions.MaxQueryTransmitTimes)
        {
            Logger.LogDebug(
                "Transmit transaction query failed after retry {Count} times. Chain id:{FromId},Target Chain: {Id}, TxId: {TxId},RoundId:{RoundId}",
                _retryTransmitInfoOptions.MaxQueryTransmitTimes, args.ChainId, args.TargetChainId, args.TransactionId,
                args.RoundId);
            PushFailedTransmitAsync(args);
        }
        else
        {
            args.QueryTimes += 1;
            try
            {
                var receipt = await _blockchainService.GetTransactionReceiptAsync(chainAlias, args.TransactionId);
                if (receipt == null || receipt.Status != TransactionStatus.Success)
                {
                    Logger.LogDebug(
                        "Transmit transaction query failed. Chain: {Id}, Target Chain: {TargetId}, TxId: {TxId},RoundId:{RoundId}",
                        args.ChainId, args.TargetChainId, args.TransactionId, args.RoundId);
                    await _backgroundJobManager.EnqueueAsync(args,
                        delay: TimeSpan.FromMinutes(_retryTransmitInfoOptions.RetryTransmitCheckTimePeriod));
                }
                else
                {
                    var currentHeight = await _blockchainService.GetBlockNumberAsync(chainAlias);
                    if (receipt.BlockNumber >=
                        currentHeight - _blockConfirmationOptions.ConfirmationCount[args.TargetChainId])
                    {
                        Logger.LogDebug(
                            "Block is not confirmed.FromChainId:{Id},TargetChainId:{TargetId},SwapId:{SwapId},RoundId:{RoundId},CurrentHeight:{CurrentHeight},BlockNumber:{BlockNumber}",
                            args.ChainId, args.TargetChainId, args.SwapId, args.RoundId, currentHeight,
                            receipt.BlockNumber);
                        await _backgroundJobManager.EnqueueAsync(args,
                            delay: TimeSpan.FromSeconds(_retryTransmitInfoOptions.RetryCheckLib));
                    }
                    else
                    {
                        var block = await _blockchainService.GetBlockByNumberAsync(chainAlias, receipt.BlockNumber);
                        if (block.BlockHash != receipt.BlockHash)
                        {
                            Logger.LogError(
                                "Transmit transaction forked.From chain:{FromId},Target Chain: {Id},TxId: {TxId}",
                                args.ChainId, args.TargetChainId, args.TransactionId);
                            PushFailedTransmitAsync(args);
                        }
                        else
                        {
                            Logger.LogInformation("Transmit transaction finished. TxId: {Id},SwapId:{SwapId},RoundId:{RoundId}", args.TransactionId,args.SwapId,args.RoundId);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(
                    "Send transmit check transaction Failed,From chain:{FromId},Target Chain: {ChainId},Swap id:{SwapId},RoundId:{RoundId},TxId:{Id}. Message: {Message}",
                    args.ChainId, args.TargetChainId, args.SwapId, args.RoundId,args.TransactionId, e);
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