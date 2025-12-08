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
    private readonly IChainProvider _chainProvider;
    private readonly ILogger<IrreversibleBlockFoundProcessor> _logger;
    
    public IrreversibleBlockFoundProcessor(
        IChainProvider chainProvider, 
        ILogger<IrreversibleBlockFoundProcessor> logger)
    {
        _chainProvider = chainProvider;
        _logger = logger;
    }
    
    public async Task ProcessAsync(string aelfChainId, long libHeight)
    {
        _logger.LogInformation("Irreversible block found, chain id: {Id}, height: {Height}", aelfChainId, libHeight);
        var chainId = _chainProvider.GetChainId(aelfChainId);
        await _chainProvider.SetLastIrreversibleBlock(chainId, libHeight);
    }
}