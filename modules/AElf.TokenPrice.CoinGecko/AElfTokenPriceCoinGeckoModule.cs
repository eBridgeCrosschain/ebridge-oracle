using CoinGecko.Clients;
using CoinGecko.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Caching;
using Volo.Abp.Modularity;

namespace AElf.TokenPrice.CoinGecko
{
    [DependsOn(typeof(AbpCachingModule))]
    public class AElfTokenPriceCoinGeckoModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            
            context.Services.AddSingleton<ICoinGeckoClient>(new CoinGeckoClient(configuration["CoinGecko:ApiKey"]));
        }
    }
}