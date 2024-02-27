using System.Collections.Concurrent;
using AElf.Nethereum.Core.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Core;

public interface ITronClientProvider : IClientProvider
{
    TronClient.TronClient GetClient(string clientAlias, string accountAlias = null);
}

public class TronClientProvider : ITronClientProvider, ISingletonDependency
{
    private readonly TronClientOptions _tronClientOptions;

    public TronClientProvider(IOptionsSnapshot<TronClientOptions> tronClientOptions)
    {
        _tronClientOptions = tronClientOptions.Value;
    }

    public TronClient.TronClient GetClient(string clientAlias, string accountAlias = null)
    {
        var clientConfig = _tronClientOptions.ClientConfigList
            .FirstOrDefault(o => o.Alias == clientAlias);
        
        if(clientConfig == null)
            throw new Exception($"Client config not found for alias {clientAlias}.");
        
        var client = new TronClient.TronClient(clientConfig.Url, clientConfig.ApiKey);

        return client;
    }

    public List<string> GetClientAliasList()
    {
        return _tronClientOptions.ClientConfigList.Select(o => o.Alias).ToList();
    }
}
