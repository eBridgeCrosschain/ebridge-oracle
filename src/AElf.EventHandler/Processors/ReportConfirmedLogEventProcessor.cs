using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElf.Client.Bridge;
using AElf.Client.Core.Extensions;
using AElf.Client.Core.Options;
using AElf.Client.Report;
using AElf.Contracts.Report;
using AElf.EventHandler.Dto;
using AElf.Nethereum.Core.Options;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace AElf.EventHandler;

internal class ReportConfirmedLogEventProcessor : LogEventProcessorBase<ReportConfirmed>
{
    public override string ContractName => "ReportContract";
    private readonly ILogger<ReportConfirmedLogEventProcessor> _logger;
    private readonly ISignatureRecoverableInfoProvider _signaturesRecoverableInfoProvider;
    private readonly BridgeOptions _bridgeOptions;
    private readonly IReportService _reportService;
    private readonly IBridgeService _bridgeService;
    private readonly IDistributedEventBus _distributedEventBus;

    public ReportConfirmedLogEventProcessor(
        ILogger<ReportConfirmedLogEventProcessor> logger,
        IOptionsSnapshot<AElfContractOptions> contractAddressOptions,
        ISignatureRecoverableInfoProvider signaturesRecoverableInfoProvider,
        IOptionsSnapshot<BridgeOptions> bridgeOptions, 
        IReportService reportService,
        IBridgeService bridgeService, 
        IDistributedEventBus distributedEventBus) : base(contractAddressOptions)
    {
        _logger = logger;
        _signaturesRecoverableInfoProvider = signaturesRecoverableInfoProvider;
        _bridgeOptions = bridgeOptions.Value;
        _reportService = reportService;
        _bridgeService = bridgeService;
        _distributedEventBus = distributedEventBus;
    }

    public override async Task ProcessAsync(LogEvent logEvent, EventContext context)
    {
        var reportConfirmed = new ReportConfirmed();
        reportConfirmed.MergeFrom(logEvent);
        _logger.LogInformation(reportConfirmed.ToString());
        var chainId = ChainIdProvider.GetChainId(context.ChainId);
        var targetChainId = reportConfirmed.TargetChainId;
        var ethereumContractAddress = reportConfirmed.Token;
        var roundId = reportConfirmed.RoundId;

        //TODO:check permission
        await _signaturesRecoverableInfoProvider.SetSignatureAsync(chainId, ethereumContractAddress, roundId,
            reportConfirmed.Signature);
        if (reportConfirmed.IsAllNodeConfirmed)
        {
            if (_bridgeOptions.IsTransmitter)
            {
                var report = await _reportService.GetRawReportAsync(chainId, new GetRawReportInput
                {
                    ChainId = targetChainId,
                    Token = ethereumContractAddress,
                    RoundId = roundId
                });
                _logger.LogInformation($"Confirm raw report:{report.Value}");
                var signatureRecoverableInfos =
                    await _signaturesRecoverableInfoProvider.GetSignatureAsync(chainId,
                        ethereumContractAddress, roundId);

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
                        i.TargetChainId == targetChainId && i.OriginToken == receiptIdInfo.Symbol &&
                        i.ChainId == ChainIdProvider.GetChainId(context.ChainId)))
                    .EthereumSwapId;

                var (swapHashId, reportBytes, rs, ss, vs) =
                    TransferToEthereumParameter(ethereumSwapId, report.Value, signatureRecoverableInfos);

                _logger.LogInformation(
                    $"Try to transmit data, TargetChainId: {reportConfirmed.TargetChainId} Address: {ethereumContractAddress}  RoundId: {reportConfirmed.RoundId}");

                await _distributedEventBus.PublishAsync(new TransmitEto
                {
                    ChainId = chainId,
                    TargetContractAddress = ethereumContractAddress,
                    TargetChainId = reportConfirmed.TargetChainId,
                    Report = reportBytes,
                    Rs = rs,
                    Ss = ss,
                    RawVs = vs,
                    SwapHashId = swapHashId,
                    BlockHash = context.BlockHash,
                    BlockHeight = context.BlockNumber
                });

                await _signaturesRecoverableInfoProvider.RemoveSignatureAsync(chainId,
                    ethereumContractAddress, roundId);
            }
        }
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