using AElf.Nethereum.Core;
using Microsoft.Extensions.Logging;

namespace AElf.Nethereum.Bridge;

public class ClientProviderAggregatorBase<T> where T : IClientProvider
{
    protected readonly Dictionary<string, T> ClientProvidersDict;
    
    public ILogger<BridgeOutService> Logger { get; set; }
    
    protected ClientProviderAggregatorBase(IEnumerable<T> clientProviders)
    {
        ClientProvidersDict = new Dictionary<string, T>();
        foreach (var provider in clientProviders)
        {
            var clientAliasList = provider.GetClientAliasList();
            foreach (var chainId in clientAliasList)
            {
                ClientProvidersDict.Add(chainId, provider);
            }
        }
    }
    
    protected T GetClientProvider(string chainId)
    {
        if (ClientProvidersDict.TryGetValue(chainId, out var clientProvider))
        {
            return clientProvider;
        }
        
        var errorMessage = $"ChainId {chainId} not found";
        Logger.LogInformation(errorMessage);
        throw new Exception(errorMessage);
    }
}