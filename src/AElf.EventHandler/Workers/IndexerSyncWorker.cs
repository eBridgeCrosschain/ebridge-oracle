using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.EventHandler.IndexerSync;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElf.EventHandler.Workers;

public class IndexerSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IEnumerable<IIndexerSyncProvider> _indexerSyncProviders;
    private readonly IChainProvider _chainProvider;

    public IndexerSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IEnumerable<IIndexerSyncProvider> indexerSyncProviders, IChainProvider chainProvider) : base(timer,
        serviceScopeFactory)
    {
        _chainProvider = chainProvider;
        _indexerSyncProviders = indexerSyncProviders.ToList();
        Timer.Period = 1000 * 5;
    }
    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var chainIds = _chainProvider.GetAllChainIds().Keys;
        var tasks = chainIds.SelectMany(chainId => _indexerSyncProviders.Select(provider => provider.ExecuteAsync(chainId)));
        await tasks.WhenAll();
    }
}