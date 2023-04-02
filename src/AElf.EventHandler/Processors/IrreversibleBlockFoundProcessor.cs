using System.Threading.Tasks;
using AElf.Client.Core;
using AElf.Client.Core.Options;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public interface IIrreversibleBlockFoundProcessor
{
    Task ProcessAsync(string aelfChainId, long libHeight);
}

public class IrreversibleBlockFoundProcessor : IIrreversibleBlockFoundProcessor,ITransientDependency
{
    private readonly IAElfClientService _aelfClientService;
    private readonly AElfChainAliasOptions _chainAliasOptions;
    private readonly IChainProvider _chainProvider;
    public ILogger<IrreversibleBlockFoundProcessor> Logger { get; set; }

    public IrreversibleBlockFoundProcessor(
        IAElfClientService aelfClientService,
        IOptionsSnapshot<AElfChainAliasOptions> chainAliasOptions, IChainProvider chainProvider) 
    {
        _aelfClientService = aelfClientService;
        _chainProvider = chainProvider;
        _chainAliasOptions = chainAliasOptions.Value;

        Logger = NullLogger<IrreversibleBlockFoundProcessor>.Instance;
    }
    
    public async Task ProcessAsync(string aelfChainId, long libHeight)
    {
        Logger.LogInformation("Irreversible block found, chain id: {Id}, height: {Height}", aelfChainId, libHeight);
        var chainId = _chainProvider.GetChainId(aelfChainId);
        var clientAlias = _chainAliasOptions.Mapping[chainId];
        var block = await _aelfClientService.GetBlockByHeightAsync(clientAlias,libHeight);
        await _chainProvider.SetLastIrreversibleBlock(chainId, Hash.LoadFromHex(block.BlockHash), block.Header.Height);
    }
}