using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using Volo.Abp.Caching;

namespace AElf.EventHandler.IndexerSync;

public class OracleQueryInfoIndexerSyncProvider : IndexerSyncProviderBase
{

    public OracleQueryInfoIndexerSyncProvider(IGraphQLClient graphQlClient, IDistributedCache<string> distributedCache)
        : base(
            graphQlClient, distributedCache)
    {
    }

    protected override string SyncType { get; } = "OracleQueryInfo";

    public override async Task ExecuteAsync(string chainId)
    {
        var processedHeight = await GetSyncHeightAsync(chainId);
        var startHeight = processedHeight + 1;
        var endHeight = await GetSyncEndHeightAsync(chainId, startHeight);
        
        var data = await QueryDataAsync<OracleQueryInfoResponse>(GetRequest(chainId, startHeight, endHeight));
        if (data == null || data.OracleQueryInfo.Count == 0)
        {
            return;
        }

        foreach (var oracleQueryInfo in data.OracleQueryInfo)
        {
            await HandleDataAsync(oracleQueryInfo);
            await SetSyncHeightAsync(chainId, oracleQueryInfo.BlockHeight);
        }
    }

    private async Task HandleDataAsync(OracleQueryInfoDto data)
    {
        switch (data.Step)
        {
            case OracleStep.QueryCreated:
                break;
            case OracleStep.SufficientCommitmentsCollected:
                break;
        }
    }

    private GraphQLRequest GetRequest(string chainId, long startHeight, long endHeight)
    {
        return new GraphQLRequest
        {
            Query =
                @"query($chainId:String,$startBlockHeight:Long!,$endBlockHeight:Long!){
            oracleQueryInfo(dto: {chainId:$chainId,startBlockHeight:$startBlockHeight,endBlockHeight:$endBlockHeight}){
                    id,
                    chainId,
                    blockHash,
                    blockHeight,
                    blockTime,
                    queryId,
                    designatedNodeList,
                    oracleStep,
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
                endBlockHeight = endHeight
            }
        };
    }
}

public class OracleQueryInfoResponse
{
    public List<OracleQueryInfoDto> OracleQueryInfo { get; set; }
}

public class OracleQueryInfoDto : GraphQLDto
{
    public string QueryId { get; set; }
    public List<string> DesignatedNodeList { get; set; }
    public QueryInfoDto QueryInfo { get; set; }
    public OracleStep Step { get; set; }
}

public class QueryInfoDto
{
    public string Title { get; set; }
    public List<string> Options { get; set; }
}

public enum OracleStep
{
    QueryCreated,
    Committed,
    SufficientCommitmentsCollected,
    CommitmentRevealed,
    QueryCompleted
}