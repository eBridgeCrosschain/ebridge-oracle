using AElf.Nethereum.Bridge.EVM;
using AElf.Nethereum.Bridge.Tron;
using AElf.Nethereum.Core;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElf.Nethereum.Bridge;

[DependsOn(
    typeof(AElfNethereumClientModule)
)]
public class AElfNethereumBridgeModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.AddTransient<IClientBridgeInService, TronBridgeInService>();
        context.Services.AddTransient<IClientBridgeOutService, TronBridgeOutService>();
        context.Services.AddTransient<IClientBridgeInService, EVMBridgeInService>();
        context.Services.AddTransient<IClientBridgeOutService, TronBridgeOutService>();
    }
}