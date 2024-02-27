using AElf.Nethereum.Core.Options;
using HDWallet.Tron;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Core.Tron;

public interface ITronAccountProvider : IAccountProvider<TronWallet>
{
}

public class TronAccountProvider : ITronAccountProvider, ISingletonDependency
{
    private readonly TronAccountOptions _tronAccountOptions;

    public TronAccountProvider(IOptionsSnapshot<TronAccountOptions> optionsSnapshot)
    {
        _tronAccountOptions = optionsSnapshot.Value;

        if (_tronAccountOptions.AccountConfig == null)
            throw new Exception("TronAccount's AccountConfig is null");
        if(_tronAccountOptions.AccountConfig.PrivateKey == null)
            throw new Exception("TronAccount's AccountConfig.PrivateKey is null");
    }

    public TronWallet GetAccount(string alias = "")
    {
        return new TronWallet(_tronAccountOptions.AccountConfig.PrivateKey);
    }
}