using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.EventHandler.IndexerSync;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElf.EventHandler.Workers;

public class IndexerSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IChainAppService _chainAppService;
    private readonly IEnumerable<IIndexerSyncProvider> _indexerSyncProviders;

    public IndexerSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IEnumerable<IIndexerSyncProvider> indexerSyncProviders, IChainAppService chainAppService) : base(timer,
        serviceScopeFactory)
    {
        _chainAppService = chainAppService;
        _indexerSyncProviders = indexerSyncProviders.ToList();
        Timer.Period = 1000 * 5;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var chains = await _chainAppService.GetListAsync(new GetChainsInput
        {
            Type = BlockchainType.AElf
        });

        foreach (var chain in chains.Items)
        {
            foreach (var provider in _indexerSyncProviders)
            {
                await provider.ExecuteAsync(chain.Id);
            }
        }
    }
}