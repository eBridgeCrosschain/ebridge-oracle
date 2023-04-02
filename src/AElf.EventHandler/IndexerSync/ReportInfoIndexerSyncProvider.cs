using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Logging;
using Volo.Abp.Caching;

namespace AElf.EventHandler.IndexerSync;

public class ReportInfoIndexerSyncProvider : IndexerSyncProviderBase
{
    private readonly IReportProposedProcessor _reportProposedProcessor;
    private readonly IReportConfirmedProcessor _reportConfirmedProcessor;
    
    public ReportInfoIndexerSyncProvider(IGraphQLClient graphQlClient, IDistributedCache<string> distributedCache, IReportProposedProcessor reportProposedProcessor, IReportConfirmedProcessor reportConfirmedProcessor) : base(
        graphQlClient,distributedCache)
    {
        _reportProposedProcessor = reportProposedProcessor;
        _reportConfirmedProcessor = reportConfirmedProcessor;
    }

    protected override string SyncType { get; } = "ReportInfo";
    
    public override async Task ExecuteAsync(string chainId)
    {
        var processedHeight = await GetSyncHeightAsync(chainId);
        var startHeight = processedHeight + 1;
        
        var currentIndexHeight = await GetIndexBlockHeightAsync(chainId);
        var endHeight = GetSyncEndHeight(startHeight, currentIndexHeight);

        while (true)
        {
            var data = await QueryDataAsync<ReportInfoResponse>(GetRequest(chainId, startHeight, endHeight));
            if (data == null || data.ReportInfo.Count == 0)
            {
                await SetSyncHeightAsync(chainId, endHeight);
            }
            else
            {
                foreach (var oracleQueryInfo in data.ReportInfo)
                {
                    await HandleDataAsync(oracleQueryInfo);
                    await SetSyncHeightAsync(chainId, oracleQueryInfo.BlockHeight);
                    Logger.LogDebug("Set {Type} sync height: {Height}", SyncType, oracleQueryInfo.BlockHeight);
                }
            }
            
            if (endHeight >= currentIndexHeight)
            {
                break;
            }
            
            startHeight = endHeight + 1;
            endHeight = GetSyncEndHeight(startHeight, currentIndexHeight);
        }
    }

    private async Task HandleDataAsync(ReportInfoDto report)
    {
        switch (report.Step)
        {
            case ReportStep.Proposed:
                await _reportProposedProcessor.ProcessAsync(report.ChainId, report);
                break;
            case ReportStep.Confirmed:
                await _reportConfirmedProcessor.ProcessAsync(report.ChainId, report);
                break;
        }
    }

    private GraphQLRequest GetRequest(string chainId, long startHeight, long endHeight)
    {
        return new GraphQLRequest
        {
            Query =
                @"query($chainId:String,$startBlockHeight:Long!,$endBlockHeight:Long!){
            reportInfo(dto: {chainId:$chainId,startBlockHeight:$startBlockHeight,endBlockHeight:$endBlockHeight}){
                    id,
                    chainId,
                    blockHash,
                    blockHeight,
                    blockTime,
                    roundId,
                    token,
                    targetChainId,
                    receiptId,
                    receiptHash,
                    step,
                    rawReport,
                    signature,
                    isAllNodeConfirmed                    
            }
        }",
            Variables = new
            {
                chainId = chainId,
                startBlockHeight = startHeight,
                endBlockHeight = endHeight
            }
        };
    }
}

public class ReportInfoResponse
{
    public List<ReportInfoDto> ReportInfo { get; set; }
}

public class ReportInfoDto : GraphQLDto
{
    public long RoundId { get; set; }
    public string Token { get; set; }
    public string TargetChainId { get; set; }
    public string ReceiptId { get; set; }
    public string ReceiptHash { get; set; }
    public ReportStep Step { get; set; }
    public string RawReport { get; set; }
    public string Signature { get; set; }
    public bool IsAllNodeConfirmed { get; set; }
}

public enum ReportStep
{
    Proposed = 0,
    Confirmed = 1
}