using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElf.PriceWorker.PriceSync;

public class PriceSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly List<IPriceSyncProvider> _priceSyncProviders;

    public PriceSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<PriceSyncOptions> priceSyncOptions, IEnumerable<IPriceSyncProvider> priceSyncProviders) : base(
        timer, serviceScopeFactory)
    {
        _priceSyncProviders = priceSyncProviders.ToList();
        Timer.Period = 1000 * priceSyncOptions.Value.SyncInterval;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("Price sync started.");
        foreach (var priceSyncProvider in _priceSyncProviders)
        {
            
            try
            {
                await priceSyncProvider.ExecuteAsync();
            }
            catch (Exception e)
            {
                Logger.LogError(e,"Price sync failed.");
            }
        }
    }
}