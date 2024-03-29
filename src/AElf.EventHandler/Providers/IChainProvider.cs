using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Types;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public interface IChainProvider
{
    string GetChainId(string aelfChainId);
    Dictionary<string, string> GetAllChainIds();
    Task SetLastIrreversibleBlock(string chainId, long blockHeight);
    Task<BlockIndex> GetLastIrreversibleBlock(string chainId);
}

public class ChainProvider : IChainProvider, ITransientDependency
{
    private readonly ChainIdMappingOptions _chainIdMappingOptions;
    private readonly IDistributedCache<BlockIndex> _distributedCache;

    public ChainProvider(IOptionsSnapshot<ChainIdMappingOptions> chainIdMappingOptions,
        IDistributedCache<BlockIndex> distributedCache)
    {
        _distributedCache = distributedCache;
        _chainIdMappingOptions = chainIdMappingOptions.Value;
    }

    public string GetChainId(string aelfChainId)
    {
        return _chainIdMappingOptions.Mapping[aelfChainId];
    }

    public Dictionary<string, string> GetAllChainIds()
    {
        return _chainIdMappingOptions.Mapping;
    }

    public async Task SetLastIrreversibleBlock(string chainId, long blockHeight)
    {
        await _distributedCache.SetAsync(GetLibCacheKey(chainId), new BlockIndex
        {
            BlockHeight = blockHeight
        }, new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.MaxValue
        });
    }

    public async Task<BlockIndex> GetLastIrreversibleBlock(string chainId)
    {
        return await _distributedCache.GetAsync(GetLibCacheKey(chainId));
    }
    
    private string GetLibCacheKey(string chainId)
    {
        return $"Lib-{chainId}";
    }
}

public class BlockIndex
{
    public long BlockHeight { get; set; }
}