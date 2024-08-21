using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

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
        var result = await _apiClient.GetAsync<BaseGasTracker>(
            $"https://eth.blockscout.com/api/v2/stats?apikey={_chainExplorerApiOptions.ApiKeys[BlockChain]}");
        Logger.LogDebug("Base gas:{s}",result.GasPrices.Average);
        return new TransactionFeeDto
        {
            Symbol = "ETH",
            Fee = decimal.Parse(result.GasPrices.Average)
        };
    }
}

public class BaseGasTracker
{
    [JsonProperty("gas_prices")]
    public GasPrices GasPrices { get; set; }
}

public class GasPrices
{
    [JsonProperty("slow")]
    public string Slow { get; set; }
    [JsonProperty("average")]
    public string Average { get; set; }
    [JsonProperty("fast")]
    public string Fast { get; set; }
}