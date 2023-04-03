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
    public ILogger<IrreversibleBlockFoundProcessor> Logger { get; set; }

    public IrreversibleBlockFoundProcessor(IChainProvider chainProvider) 
    {
        _chainProvider = chainProvider;

        Logger = NullLogger<IrreversibleBlockFoundProcessor>.Instance;
    }
    
    public async Task ProcessAsync(string aelfChainId, long libHeight)
    {
        Logger.LogInformation("Irreversible block found, chain id: {Id}, height: {Height}", aelfChainId, libHeight);
        var chainId = _chainProvider.GetChainId(aelfChainId);
        await _chainProvider.SetLastIrreversibleBlock(chainId, libHeight);
    }
}