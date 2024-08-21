using Microsoft.Extensions.Options;

namespace AElf.BlockchainTransactionFee;

public class BaseTransactionFeeProvider : IBlockchainTransactionFeeProvider
{
    public string BlockChain { get; } = "Base";
    
    private readonly ApiClient _apiClient;

    private readonly ChainExplorerApiOptions _chainExplorerApiOptions;

    public BaseTransactionFeeProvider(ApiClient apiClient,
        IOptionsSnapshot<ChainExplorerApiOptions> blockchainExplorerApiOptions)
    {
        _apiClient = apiClient;
        _chainExplorerApiOptions = blockchainExplorerApiOptions.Value;
    }
    
    public async Task<TransactionFeeDto> GetTransactionFee()
    {
        var result = await _apiClient.GetAsync<BaseApiResult<BaseGasTracker>>(
            $"https://eth.blockscout.com/api/v2/stats?apikey={_chainExplorerApiOptions.ApiKeys[BlockChain]}");
        if (result.Message != "OK")
        {
            throw new HttpRequestException($"Base api failed: {result.Message}");
        }

        return new TransactionFeeDto
        {
            Symbol = "ETH",
            Fee = decimal.Parse(result.Result.GasPrices.Average)
        };
    }
}

public class BaseApiResult<T>
{
    public string Message { get; set; }

    public T Result { get; set; }
}

public class BaseGasTracker
{
    public GasPrices GasPrices { get; set; }
}

public class GasPrices
{
    public string Slow { get; set; }
    public string Average { get; set; }
    public string Fast { get; set; }
    
}