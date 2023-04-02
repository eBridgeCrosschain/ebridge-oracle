using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler.IndexerSync;

public abstract class IndexerSyncProviderBase : IIndexerSyncProvider, ITransientDependency
{
    protected readonly IGraphQLClient GraphQlClient;
    private readonly IDistributedCache<string> _distributedCache;

    public ILogger<IndexerSyncProviderBase> Logger { get; set; }

    protected const int MaxRequestCount = 1000;

    protected IndexerSyncProviderBase(IGraphQLClient graphQlClient, IDistributedCache<string> distributedCache)
    {
        GraphQlClient = graphQlClient;
        _distributedCache = distributedCache;
        Logger = NullLogger<IndexerSyncProviderBase>.Instance;
    }

    // protected async Task<long> GetSyncEndHeightAsync(string chainId, long startHeight)
    // {
    //     var currentIndexHeight = await GetIndexBlockHeightAsync(chainId);
    //     return Math.Min(startHeight + MaxRequestCount - 1, currentIndexHeight);
    // }
    protected long GetSyncEndHeight(long startHeight, long currentIndexHeight)
    {
        return Math.Min(startHeight + MaxRequestCount - 1, currentIndexHeight);
    }
    

    protected async Task<T> QueryDataAsync<T>(GraphQLRequest request)
    {
        var data = await GraphQlClient.SendQueryAsync<T>(request);
        if (data.Errors == null || data.Errors.Length == 0)
        {
            return data.Data;
        }

        Logger.LogError("Query indexer failed. errors: {Errors}",
            string.Join(",", data.Errors.Select(e => e.Message).ToList()));
        throw new Exception("Query indexer failed.");
    }

    protected async Task<long> GetIndexBlockHeightAsync(string chainId)
    {
        var data = await QueryDataAsync<ConfirmedBlockHeight>(new GraphQLRequest
        {
            Query = @"
			    query($chainId:String,$filterType:BlockFilterType!) {
                    syncState(dto: {chainId:$chainId,filterType:$filterType}){
                        confirmedBlockHeight}
                    }",
            Variables = new
            {
                chainId,
                filterType = BlockFilterType.LOG_EVENT
            }
        });

        return data.SyncState.ConfirmedBlockHeight;
    }
    
    protected async Task<long> GetSyncHeightAsync(string chainId)
    {
        var height = await _distributedCache.GetAsync(GetSyncHeightKey(chainId));
        return height == null ? 0 : long.Parse(height);
    }

    protected async Task SetSyncHeightAsync(string chainId, long height)
    {
        await _distributedCache.SetAsync(GetSyncHeightKey(chainId), height.ToString());
    }

    private string GetSyncHeightKey(string chainId)
    {
        return $"IndexerSync-{chainId}-{SyncType}";
    }

    protected abstract string SyncType { get; }

    public abstract Task ExecuteAsync(string chainId);
}

public class ConfirmedBlockHeight
{
    public SyncState SyncState { get; set; }
}

public class SyncState
{
    public long ConfirmedBlockHeight { get; set; }
}

public enum BlockFilterType
{
    BLOCK,
    TRANSACTION,
    LOG_EVENT
}
