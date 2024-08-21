using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AElf.BlockchainTransactionFee;

public class BaseTransactionFeeProvider : IBlockchainTransactionFeeProvider
{
    public string BlockChain { get; } = "Base";
    
    private readonly ApiClient _apiClient;

    private readonly ChainExplorerApiOptions _chainExplorerApiOptions;
    public ILogger<BaseTransactionFeeProvider> Logger { get; set; }


    public BaseTransactionFeeProvider(ApiClient apiClient,
        IOptionsSnapshot<ChainExplorerApiOptions> blockchainExplorerApiOptions)
    {
        _apiClient = apiClient;
        _chainExplorerApiOptions = blockchainExplorerApiOptions.Value;
        Logger = NullLogger<BaseTransactionFeeProvider>.Instance;
    }
    
    public async Task<TransactionFeeDto> GetTransactionFee()
    {
        Logger.LogDebug("Get base chain transaction fee.");
        var result = await _apiClient.GetAsync<BaseApiResult<BaseGasTracker>>(
            $"https://eth.blockscout.com/api/v2/stats?apikey={_chainExplorerApiOptions.ApiKeys[BlockChain]}");
        Logger.LogDebug("status:{s}",result.Message);
        if (result.Message != "OK")
        {
            throw new HttpRequestException($"Base api failed: {result.Message}");
        }
        Logger.LogDebug("Base gas:{s}",result.Result.GasPrices.Average);
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