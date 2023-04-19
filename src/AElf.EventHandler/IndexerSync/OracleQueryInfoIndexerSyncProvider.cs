using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;

namespace AElf.EventHandler.IndexerSync;

public class OracleQueryInfoIndexerSyncProvider : IndexerSyncProviderBase
{
    private readonly IQueryCreatedProcessor _queryCreatedProcessor;
    private readonly ISufficientCommitmentsCollectedProcessor _sufficientCommitmentsCollectedProcessor;

    public OracleQueryInfoIndexerSyncProvider(IGraphQLClient graphQlClient, IDistributedCache<string> distributedCache,
        IQueryCreatedProcessor queryCreatedProcessor,
        ISufficientCommitmentsCollectedProcessor sufficientCommitmentsCollectedProcessor,
        IOptionsSnapshot<IndexerSyncOptions> indexerSyncOptions)
        : base(
            graphQlClient, distributedCache, indexerSyncOptions)
    {
        _queryCreatedProcessor = queryCreatedProcessor;
        _sufficientCommitmentsCollectedProcessor = sufficientCommitmentsCollectedProcessor;
    }

    protected override string SyncType { get; } = "OracleQueryInfo";

    public override async Task ExecuteAsync(string chainId)
    {
        var processedHeight = await GetSyncHeightAsync(chainId);
        var startHeight = processedHeight + 1;
        
        var currentIndexHeight = await GetIndexBlockHeightAsync(chainId);
        var endHeight = GetSyncEndHeight(startHeight, currentIndexHeight);

        while (!IsSyncFinished(startHeight, currentIndexHeight))
        {
            var data = await QueryDataAsync<OracleQueryInfoResponse>(GetRequest(chainId, startHeight, endHeight));
            if (data != null && data.OracleQueryInfo.Count > 0)
            {
                foreach (var oracleQueryInfo in data.OracleQueryInfo)
                {
                    Logger.LogDebug("Handle {Type}, sync height: {Height}", SyncType, oracleQueryInfo.BlockHeight);
                    await HandleDataAsync(oracleQueryInfo);
                }
            }
            
            await SetSyncHeightAsync(chainId, endHeight);

            startHeight = endHeight + 1;
            endHeight = GetSyncEndHeight(startHeight, currentIndexHeight);
        }
    }

    private async Task HandleDataAsync(OracleQueryInfoDto data)
    {
        switch (data.Step)
        {
            case OracleStep.QUERY_CREATED:
                await _queryCreatedProcessor.ProcessAsync(data.ChainId, data);
                break;
            case OracleStep.SUFFICIENT_COMMITMENTS_COLLECTED:
                await _sufficientCommitmentsCollectedProcessor.ProcessAsync(data.ChainId, data);
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
                    step,
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
    QUERY_CREATED,
    COMMITTED,
    SUFFICIENT_COMMITMENTS_COLLECTED,
    COMMITMENT_REVEALED,
    QUERY_COMPLETED
}