using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Bridge;

public interface IBridgeOutService
{
    Task<string> TransmitAsync(string chainId, string contractAddress, byte[] swapHashId, byte[] report, byte[][] rs, byte[][] ss, byte[] rawVs);
}

public class BridgeOutService : ClientProviderAggregatorBase<IClientBridgeOutService>, IBridgeOutService, ITransientDependency
{
    public BridgeOutService(IEnumerable<IClientBridgeOutService> bridgeOutServices) : base(bridgeOutServices)
    {
    }
    
    public async Task<string> TransmitAsync(string chainId, string contractAddress, byte[] swapHashId, byte[] report,
        byte[][] rs, byte[][] ss, byte[] rawVs)
    {
        var clientProvider = GetClientProvider(chainId);
        return await clientProvider.TransmitAsync(chainId, contractAddress, swapHashId, report, rs, ss, rawVs);
    }
}