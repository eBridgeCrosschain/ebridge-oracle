using System.Threading.Tasks;
using AElf.EventHandler.HttpClientHelper;
using AElf.EventHandler.Options;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;

namespace AElf.EventHandler.IndexerSync;

public class IrreversibleBlockHeightIndexerSyncProvider : IndexerSyncProviderBase
{
    private readonly IIrreversibleBlockFoundProcessor _irreversibleBlockFoundProcessor;

    public IrreversibleBlockHeightIndexerSyncProvider(IGraphQLClient graphQlClient,
        IDistributedCache<string> distributedCache, IIrreversibleBlockFoundProcessor irreversibleBlockFoundProcessor,
        IOptionsSnapshot<IndexerSyncOptions> indexerSyncOptions,ApiClient apiClient,IOptionsSnapshot<SyncStateServiceOption> syncStateServiceOption)
        : base(
            graphQlClient, distributedCache, indexerSyncOptions,apiClient,syncStateServiceOption)
    {
        _irreversibleBlockFoundProcessor = irreversibleBlockFoundProcessor;
    }

    protected override string SyncType { get; } = "IrreversibleBlockHeight";

    public override async Task ExecuteAsync(string chainId)
    {
        var processedHeight = await GetSyncHeightAsync(chainId);
        var latestIndexBlockHeight = await GetIndexBlockHeightAsync(chainId);
        if (latestIndexBlockHeight > processedHeight)
        {
            await _irreversibleBlockFoundProcessor.ProcessAsync(chainId, latestIndexBlockHeight);
            await SetSyncHeightAsync(chainId, latestIndexBlockHeight);
        }
    }
}
