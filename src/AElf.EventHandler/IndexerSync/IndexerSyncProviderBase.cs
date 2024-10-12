using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AElf.EventHandler.HttpClientHelper;
using AElf.EventHandler.Options;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler.IndexerSync;

public abstract class IndexerSyncProviderBase : IIndexerSyncProvider, ITransientDependency
{
    protected readonly IGraphQLClient GraphQlClient;
    private readonly IDistributedCache<string> _distributedCache;
    protected readonly IndexerSyncOptions IndexerSyncOptions;
    private readonly ApiClient _apiClient;
    private readonly SyncStateServiceOption _syncStateServiceOption;

    public ILogger<IndexerSyncProviderBase> Logger { get; set; }

    protected const int MaxRequestCount = 1000;
    protected const int SyncDelayLimit = 100;
    private ApiInfo _syncStateUri => new (HttpMethod.Get, _syncStateServiceOption.SyncStateUri);

    protected IndexerSyncProviderBase(IGraphQLClient graphQlClient, IDistributedCache<string> distributedCache,
        IOptionsSnapshot<IndexerSyncOptions> indexerSyncOptions, ApiClient apiClient,IOptionsSnapshot<SyncStateServiceOption> syncStateServiceOption)
    {
        GraphQlClient = graphQlClient;
        _distributedCache = distributedCache;
        _apiClient = apiClient;
        IndexerSyncOptions = indexerSyncOptions.Value;
        Logger = NullLogger<IndexerSyncProviderBase>.Instance;
        _syncStateServiceOption = syncStateServiceOption.Value;
    }

    protected long GetSyncEndHeight(long startHeight, long currentIndexHeight)
    {
        return Math.Min(startHeight + MaxRequestCount - 1, currentIndexHeight - SyncDelayLimit);
    }
    
    protected bool IsSyncFinished(long startHeight, long currentIndexHeight)
    {
        return startHeight >= currentIndexHeight - SyncDelayLimit;
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
        var res = await _apiClient.GetAsync<SyncStateResponse>(_syncStateServiceOption.BaseUrl+_syncStateUri.Path);
        var blockHeight= res.CurrentVersion.Items.FirstOrDefault(i => i.ChainId == chainId)?.LastIrreversibleBlockHeight;
        Logger.LogInformation("Get latest index height. chainId: {chainId}, height: {height}",chainId,blockHeight);
        return blockHeight ?? 0;
    }
    
    protected async Task<long> GetSyncHeightAsync(string chainId)
    {
        var height = await _distributedCache.GetAsync(GetSyncHeightKey(chainId));
        return height == null ? IndexerSyncOptions.StartSyncHeight[chainId] : long.Parse(height);
    }

    protected async Task SetSyncHeightAsync(string chainId, long height)
    {
        await _distributedCache.SetAsync(GetSyncHeightKey(chainId), height.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.MaxValue
        });
    }

    private string GetSyncHeightKey(string chainId)
    {
        return $"IndexerSync-{chainId}-{SyncType}";
    }

    protected abstract string SyncType { get; }

    public abstract Task ExecuteAsync(string chainId);
}