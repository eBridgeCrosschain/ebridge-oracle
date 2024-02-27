using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.BlockchainTransactionFee;

public class TronTransactionFeeProvider : IBlockchainTransactionFeeProvider, ITransientDependency
{
    public string BlockChain { get; } = "Tron";

    private readonly TronClient.TronClient _tronClient;

    public TronTransactionFeeProvider(IOptionsSnapshot<ChainExplorerApiOptions> blockchainExplorerApiOptions)
    {
        _tronClient = new TronClient.TronClient(blockchainExplorerApiOptions.Value.Url[BlockChain], blockchainExplorerApiOptions.Value.ApiKeys[BlockChain]);
    }

    public async Task<TransactionFeeDto> GetTransactionFee()
    {
        var latestEnergyPrice = await _tronClient.GetLatestEnergyPrice();

        return new TransactionFeeDto
        {
            Symbol = "TRX",
            FeeInSmallestUnit = latestEnergyPrice
        };
    }
}