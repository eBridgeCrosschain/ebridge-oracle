using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public interface IChainIdProvider
{
    string GetChainId(string aelfChainId);
}

public class ChainIdProvider : IChainIdProvider, ITransientDependency
{
    private readonly ChainIdMappingOptions _chainIdMappingOptions;

    public ChainIdProvider(IOptionsSnapshot<ChainIdMappingOptions> chainIdMappingOptions)
    {
        _chainIdMappingOptions = chainIdMappingOptions.Value;
    }

    public string GetChainId(string aelfChainId)
    {
        return _chainIdMappingOptions.Mapping[aelfChainId];
    }
}