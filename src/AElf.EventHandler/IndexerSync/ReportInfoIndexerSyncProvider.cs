using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.EventHandler.HttpClientHelper;
using AElf.EventHandler.Options;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;

namespace AElf.EventHandler.IndexerSync;

public class ReportInfoIndexerSyncProvider : IndexerSyncProviderBase
{
    private readonly IReportProposedProcessor _reportProposedProcessor;
    private readonly IReportConfirmedProcessor _reportConfirmedProcessor;

    public ReportInfoIndexerSyncProvider(IGraphQLClient graphQlClient, IDistributedCache<string> distributedCache,
        IReportProposedProcessor reportProposedProcessor, IReportConfirmedProcessor reportConfirmedProcessor,
        IOptionsSnapshot<IndexerSyncOptions> indexerSyncOptions,ApiClient apiClient,IOptionsSnapshot<SyncStateServiceOption> syncStateServiceOption)
        : base(
            graphQlClient, distributedCache, indexerSyncOptions,apiClient,syncStateServiceOption)
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
        Logger.LogDebug("Handle {Type}, currentIndexHeight: {Height}", SyncType, currentIndexHeight);
        var endHeight = GetSyncEndHeight(startHeight, currentIndexHeight);
        Logger.LogDebug("Handle {Type}, start height: {Height}, end height: {EndHeight}", SyncType, startHeight,endHeight);

        while (!IsSyncFinished(startHeight, currentIndexHeight))
        {
            var data = await QueryDataAsync<ReportInfoResponse>(GetRequest(chainId, startHeight, endHeight));
            if (data != null && data.ReportInfo.Count > 0)
            {
                foreach (var oracleQueryInfo in data.ReportInfo)
                {
                    Logger.LogDebug("Handle {Type}, sync height: {Height}", SyncType, oracleQueryInfo.BlockHeight);
                    await HandleDataAsync(oracleQueryInfo);
                }
            }
            
            await SetSyncHeightAsync(chainId, endHeight);

            startHeight = endHeight + 1;
            endHeight = GetSyncEndHeight(startHeight, currentIndexHeight);
            Logger.LogDebug("Handle {Type}, next start height: {Height}, end height: {EndHeight}", SyncType, startHeight,endHeight);

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
                @"query($chainId:String,$startBlockHeight:Long!,$endBlockHeight:Long!,$maxMaxResultCount:Int!){
            reportInfo(input: {chainId:$chainId,startBlockHeight:$startBlockHeight,endBlockHeight:$endBlockHeight,maxMaxResultCount:$maxMaxResultCount}){
                    id,
                    chainId,
                    blockHash,
                    blockHeight,
                    blockTime,
                    roundId,
                    token,
                    targetChainId,
                    step,
                    rawReport,
                    signature,
                    isAllNodeConfirmed,
                    queryInfo{
                        title,
                        options
                    }                    
            }
        }",
            Variables = new
            {
                chainId = chainId,
                startBlockHeight = startHeight,
                endBlockHeight = endHeight,
                maxMaxResultCount = MaxRequestCount
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
    public ReportStep Step { get; set; }
    public string RawReport { get; set; }
    public string Signature { get; set; }
    public bool IsAllNodeConfirmed { get; set; }
    public OffChainQueryInfoDto QueryInfo { get; set; }
}

public enum ReportStep
{
    Proposed = 0,
    Confirmed = 1
}

public class OffChainQueryInfoDto
{
    public string Title { get; set; }
    public List<string> Options { get; set; }
}