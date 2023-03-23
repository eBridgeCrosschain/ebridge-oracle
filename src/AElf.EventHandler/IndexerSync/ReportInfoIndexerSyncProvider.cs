using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using Volo.Abp.Caching;

namespace AElf.EventHandler.IndexerSync;

public class ReportInfoIndexerSyncProvider : IndexerSyncProviderBase
{

    public ReportInfoIndexerSyncProvider(IGraphQLClient graphQlClient, IDistributedCache<string> distributedCache) : base(
        graphQlClient,distributedCache)
    {
    }

    protected override string SyncType { get; } = "ReportInfo";
    
    public override async Task ExecuteAsync(string chainId)
    {
        var processedHeight = await GetSyncHeightAsync(chainId);
        var startHeight = processedHeight + 1;
        var endHeight = await GetSyncEndHeightAsync(chainId, startHeight);
        
        var data = await QueryDataAsync<ReportInfoResponse>(GetRequest(chainId, startHeight, endHeight));
        if (data == null || data.ReportInfo.Count == 0)
        {
            return;
        }

        foreach (var oracleQueryInfo in data.ReportInfo)
        {
            await HandleDataAsync(oracleQueryInfo);
            await SetSyncHeightAsync(chainId, oracleQueryInfo.BlockHeight);
        }
    }

    private async Task HandleDataAsync(ReportInfoDto report)
    {
        switch (report.Step)
        {
            case ReportStep.Proposed:
                break;
            case ReportStep.Confirmed:
                break;
        }
    }

    private GraphQLRequest GetRequest(string chainId, long startHeight, long endHeight)
    {
        return new GraphQLRequest
        {
            Query =
                @"query($chainId:String,$startBlockHeight:Long!,$endBlockHeight:Long!,$methodNames: [String],$skipCount:Int!,$maxResultCount:Int!){
            caHolderTransactionInfo(dto: {chainId:$chainId,startBlockHeight:$startBlockHeight,endBlockHeight:$endBlockHeight, methodNames:$methodNames,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                totalRecordCount,
                data{
                    blockHash,
                    blockHeight,
                    transactionId,
                    methodName,
                    transferInfo{
                        fromChainId,
                        toChainId
                    }
                }
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