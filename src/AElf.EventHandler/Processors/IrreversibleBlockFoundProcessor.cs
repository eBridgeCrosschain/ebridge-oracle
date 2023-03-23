using System.Threading.Tasks;
using AElf.Client.Core;
using AElf.Client.Core.Extensions;
using AElf.Client.Core.Options;
using AElf.Contracts.Consensus.AEDPoS;
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
    private readonly ITransmitTransactionProvider _transmitTransactionProvider;
    private readonly IAElfClientService _aelfClientService;
    private readonly AElfChainAliasOptions _chainAliasOptions;
    private readonly IChainIdProvider _chainIdProvider;
    public ILogger<IrreversibleBlockFoundProcessor> Logger { get; set; }

    public IrreversibleBlockFoundProcessor(
        ITransmitTransactionProvider transmitTransactionProvider, IAElfClientService aelfClientService,
        IOptionsSnapshot<AElfChainAliasOptions> chainAliasOptions, IChainIdProvider chainIdProvider) 
    {
        _transmitTransactionProvider = transmitTransactionProvider;
        _aelfClientService = aelfClientService;
        _chainIdProvider = chainIdProvider;
        _chainAliasOptions = chainAliasOptions.Value;

        Logger = NullLogger<IrreversibleBlockFoundProcessor>.Instance;
    }
    
    public async Task ProcessAsync(string aelfChainId, long libHeight)
    {
        var chainId = _chainIdProvider.GetChainId(aelfChainId);
        var clientAlias = _chainAliasOptions.Mapping[chainId];
        var block = await _aelfClientService.GetBlockByHeightAsync(clientAlias,libHeight);
        await _transmitTransactionProvider.SendByLibAsync(chainId, block.BlockHash, block.Header.Height);
    }
}