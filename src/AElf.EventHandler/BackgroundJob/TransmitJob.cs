using System;
using System.Threading.Tasks;
using AElf.Client.Core;
using AElf.Client.Core.Options;
using AElf.EventHandler.Dto;
using AElf.EventHandler.Error;
using AElf.Nethereum.Bridge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AElf.EventHandler.BackgroundJob;

public class TransmitJob :
    AsyncBackgroundJob<TransmitArgs>, ISingletonDependency
{
    private readonly IAElfClientService _aelfClientService;
    private readonly AElfChainAliasOptions _aelfChainAliasOption;
    private readonly RetryTransmitInfoOptions _retryTransmitInfoOptions;
    private readonly IBridgeOutService _bridgeOutService;
    private readonly IObjectMapper<EventHandlerAppModule> _objectMapper;
    private readonly ITransmitTransactionProvider _transmitTransactionProvider;
    private readonly IChainProvider _chainProvider;
    private readonly BridgeOptions _bridgeOptions;
    private readonly IBackgroundJobManager _backgroundJobManager;
    public ILogger<TransmitJob> Logger { get; set; }


    public TransmitJob(
        IAElfClientService aelfClientService,
        IOptionsSnapshot<AElfChainAliasOptions> aelfChainAliasOption,
        IOptionsSnapshot<RetryTransmitInfoOptions> retryTransmitInfoOptions,
        IOptionsSnapshot<BridgeOptions> bridgeOptions,
        IBridgeOutService bridgeOutService,
        IObjectMapper<EventHandlerAppModule> objectMapper,
        ITransmitTransactionProvider transmitTransactionProvider,
        IChainProvider chainProvider,
        IBackgroundJobManager backgroundJobManager)
    {
        _aelfClientService = aelfClientService;
        _bridgeOutService = bridgeOutService;
        _objectMapper = objectMapper;
        _transmitTransactionProvider = transmitTransactionProvider;
        _chainProvider = chainProvider;
        _backgroundJobManager = backgroundJobManager;
        _aelfChainAliasOption = aelfChainAliasOption.Value;
        _retryTransmitInfoOptions = retryTransmitInfoOptions.Value;
        _bridgeOptions = bridgeOptions.Value;
    }

    public override async Task ExecuteAsync(TransmitArgs args)
    {
        if (!_bridgeOptions.IsTransmitter) return;
        // Last Irreversible Block Height.
        var lib = await _chainProvider.GetLastIrreversibleBlock(args.ChainId);
        if (args.BlockHeight > lib.BlockHeight)
        {
            Logger.LogDebug("Current transaction block height is higher than lib.SwapId:{Id}", args.SwapId);
            await _backgroundJobManager.EnqueueAsync(args,
                delay: TimeSpan.FromSeconds(_retryTransmitInfoOptions.RetryCheckLib));
        }

        var block = await _aelfClientService.GetBlockByHeightAsync(_aelfChainAliasOption.Mapping[args.ChainId],
            args.BlockHeight);
        if (block?.BlockHash == args.BlockHash)
        {
            if (args.SendTimes > _retryTransmitInfoOptions.MaxSendTransmitTimes)
            {
                PushFailedTransaction(args, QueueConstants.TransmitFailedList);
            }
            else
            {
                args.SendTimes += 1;
                try
                {
                    var sendResult = await _bridgeOutService.TransmitAsync(args.TargetChainId,
                        args.TargetContractAddress, args.SwapHashId, args.Report, args.Rs,
                        args.Ss,
                        args.RawVs);
                    if (string.IsNullOrWhiteSpace(sendResult))
                    {
                        Logger.LogError(
                            "Failed to transmit,chainId:{Chain},target chain id:{TargetChainId},swapId:{Id},roundId:{RoundId}",
                            args.ChainId, args.TargetChainId,
                            args.SwapId, args.RoundId);
                        await _backgroundJobManager.EnqueueAsync(args,
                            delay: TimeSpan.FromMinutes(_retryTransmitInfoOptions.RetryTransmitTimePeriod));
                    }
                    else
                    {
                        args.TransactionId = sendResult;
                        await _backgroundJobManager.EnqueueAsync(
                            _objectMapper.Map<TransmitArgs, TransmitCheckArgs>(args),
                            delay: TimeSpan.FromMinutes(_retryTransmitInfoOptions.RetryTransmitTimePeriod));
                        Logger.LogInformation(
                            "Send transmit check transaction. TxId: {Result},chainId:{Chain},target chain id:{TargetChainId},swapId:{Id},roundId:{RoundId}",
                            sendResult,
                            args.ChainId, args.TargetChainId, args.SwapId, args.RoundId);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(
                        "Send Transmit transaction Failed,chainId:{Chain},target chain id:{TargetChainId},swapId:{Id},roundId:{RoundId}. Message: {Message}",
                        args.ChainId, args.TargetChainId, args.SwapId, args.RoundId, e.Message);
                    DealWithErrorMessage(e.Message, args);
                }
            }
        }
    }

    private async void DealWithErrorMessage(string errorMessage, TransmitArgs args)
    {
        if (errorMessage.Contains(TransactionErrorConstants.AlreadyClaimed))
        {
           return;
        }

        if (errorMessage.Contains(TransactionErrorConstants.DailyLimitExceeded))
        {
            PushFailedTransaction(args, QueueConstants.ExceedDailyLimitList);
        }
        else
        {
            await _backgroundJobManager.EnqueueAsync(args,
                delay: TimeSpan.FromMinutes(_retryTransmitInfoOptions.RetryTransmitTimePeriod));
        }
    }

    private async void PushFailedTransaction(TransmitArgs eventData, string queue)
    {
        //redis
        eventData.SendTimes = 0;
        eventData.Time = DateTime.UtcNow;
        await _transmitTransactionProvider.PushFailedTransmitAsync(eventData, queue);
    }
}