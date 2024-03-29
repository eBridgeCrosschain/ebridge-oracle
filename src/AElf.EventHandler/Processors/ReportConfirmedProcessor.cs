using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Client.Bridge;
using AElf.Client.Report;
using AElf.Contracts.Report;
using AElf.EventHandler.Dto;
using AElf.EventHandler.IndexerSync;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public interface IReportConfirmedProcessor
{
    Task ProcessAsync(string aelfChainId, ReportInfoDto reportQueryInfo);
}

public class ReportConfirmedProcessor : IReportConfirmedProcessor, ITransientDependency
{
    private readonly ILogger<ReportConfirmedProcessor> _logger;
    private readonly ISignatureRecoverableInfoProvider _signaturesRecoverableInfoProvider;
    private readonly BridgeOptions _bridgeOptions;
    private readonly IReportService _reportService;
    private readonly IBridgeService _bridgeService;
    private readonly IChainProvider _chainProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly RetryTransmitInfoOptions _retryTransmitInfoOptions;

    public ReportConfirmedProcessor(ILogger<ReportConfirmedProcessor> logger,
        ISignatureRecoverableInfoProvider signaturesRecoverableInfoProvider,
        IOptionsSnapshot<BridgeOptions> bridgeOptions, IReportService reportService,
        IBridgeService bridgeService, IChainProvider chainProvider, 
        IBackgroundJobManager backgroundJobManager,
        IOptionsSnapshot<RetryTransmitInfoOptions> retryTransmitInfoOptions)
    {
        _logger = logger;
        _signaturesRecoverableInfoProvider = signaturesRecoverableInfoProvider;
        _bridgeOptions = bridgeOptions.Value;
        _reportService = reportService;
        _bridgeService = bridgeService;
        _chainProvider = chainProvider;
        _backgroundJobManager = backgroundJobManager;
        _retryTransmitInfoOptions = retryTransmitInfoOptions.Value;
    }

    public async Task ProcessAsync(string aelfChainId, ReportInfoDto reportQueryInfo)
    {
        var chainId = _chainProvider.GetChainId(aelfChainId);
        var targetChainId = reportQueryInfo.TargetChainId;
        var ethereumContractAddress = reportQueryInfo.Token;
        var roundId = reportQueryInfo.RoundId;

        //TODO:check permission
        await _signaturesRecoverableInfoProvider.SetSignatureAsync(chainId, targetChainId,ethereumContractAddress, roundId,
            reportQueryInfo.Signature);
        if (!reportQueryInfo.IsAllNodeConfirmed) return;
        if (!_bridgeOptions.IsTransmitter) return;
        var report = await _reportService.GetRawReportAsync(chainId, new GetRawReportInput
        {
            ChainId = targetChainId,
            Token = ethereumContractAddress,
            RoundId = roundId
        });
        _logger.LogInformation("Confirm raw report:{Report}", report.Value);
        var signatureRecoverableInfos =
            await _signaturesRecoverableInfoProvider.GetSignatureAsync(chainId,targetChainId,
                ethereumContractAddress, roundId);
        foreach (var signature in signatureRecoverableInfos)
        {
            _logger.LogInformation("Log signature:RoundId:{RoundId},Signature: {Signature}", reportQueryInfo.RoundId,signature);
        }
        if (signatureRecoverableInfos.Any(o => o.IsNullOrWhiteSpace()))
        {
            _logger.LogError("Wrong signature recoverable info: {}", reportQueryInfo.RoundId);
            return;
        }

        //GetSwapId
        var receiptId = (await _reportService.GetReportAsync(chainId, new GetReportInput
        {
            ChainId = targetChainId,
            Token = ethereumContractAddress,
            RoundId = roundId
        })).Observations.Value.First().Key;
        var receiptIdTokenHash = receiptId.Split(".").First();
        var receiptIdInfo = await _bridgeService.GetReceiptIdInfoAsync(chainId,
            Hash.LoadFromHex(receiptIdTokenHash));

        var ethereumSwapId =
            (_bridgeOptions.BridgesOut.Single(i =>
                i.TargetChainId == targetChainId && i.OriginToken == receiptIdInfo.Symbol && i.ChainId == chainId))
            .EthereumSwapId;

        var (swapHashId, reportBytes, rs, ss, vs) =
            TransferToEthereumParameter(ethereumSwapId, report.Value, signatureRecoverableInfos);

        _logger.LogInformation(
            "Try to transmit data, TargetChainId: {ChainId} Address: {Address}  RoundId: {RoundId}",
            reportQueryInfo.TargetChainId, ethereumContractAddress, reportQueryInfo.RoundId);
        
        await _backgroundJobManager.EnqueueAsync(new TransmitArgs
        {
            ChainId = chainId,
            TargetContractAddress = ethereumContractAddress,
            TargetChainId = reportQueryInfo.TargetChainId,
            Report = reportBytes,
            Rs = rs,
            Ss = ss,
            RawVs = vs,
            SwapHashId = swapHashId,
            BlockHash = reportQueryInfo.BlockHash,
            BlockHeight = reportQueryInfo.BlockHeight,
            SwapId = ethereumSwapId,
            RoundId = reportQueryInfo.RoundId
        },delay:TimeSpan.FromSeconds(_retryTransmitInfoOptions.DelayTransmitTimePeriod));

        await _signaturesRecoverableInfoProvider.RemoveSignatureAsync(chainId,targetChainId,
            ethereumContractAddress, roundId);
    }

    public (byte[], byte[], byte[][], byte[][], byte[]) TransferToEthereumParameter(string swapId, string report,
        HashSet<string> recoverableInfos)
    {
        var signaturesCount = recoverableInfos.Count;
        var r = new byte[signaturesCount][];
        var s = new byte[signaturesCount][];
        var v = new byte[32];
        var index = 0;
        foreach (var recoverableInfoBytes in recoverableInfos.Select(recoverableInfo =>
                     ByteStringHelper.FromHexString(recoverableInfo).ToByteArray()))
        {
            r[index] = recoverableInfoBytes.Take(32).ToArray();
            s[index] = recoverableInfoBytes.Skip(32).Take(32).ToArray();
            v[index] = recoverableInfoBytes.Last();
            index++;
        }
        
        return (ByteStringHelper.FromHexString(swapId).ToByteArray(),
            ByteStringHelper.FromHexString(report).ToByteArray(), r, s, v);
    }
}