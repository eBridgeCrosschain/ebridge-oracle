using System.Threading.Tasks;
using AElf.Client.Oracle;
using AElf.Contracts.Oracle;
using AElf.EventHandler.IndexerSync;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public interface ISufficientCommitmentsCollectedProcessor
{
    Task ProcessAsync(string aelfChainId, OracleQueryInfoDto oracleQueryInfo);
}

public class SufficientCommitmentsCollectedProcessor :ISufficientCommitmentsCollectedProcessor,ITransientDependency
{
    private readonly ISaltProvider _saltProvider;
    private readonly IDataProvider _dataProvider;
    private readonly ILogger<SufficientCommitmentsCollectedProcessor> _logger;
    private readonly IOracleService _oracleService;
    private readonly IChainProvider _chainProvider;

    public SufficientCommitmentsCollectedProcessor(
        ISaltProvider saltProvider, IDataProvider dataProvider,
        ILogger<SufficientCommitmentsCollectedProcessor> logger,
        IOracleService oracleService, IChainProvider chainProvider)
    {
        _saltProvider = saltProvider;
        _dataProvider = dataProvider;
        _logger = logger;
        _oracleService = oracleService;
        _chainProvider = chainProvider;
    }
    
    public async Task ProcessAsync(string aelfChainId, OracleQueryInfoDto oracleQueryInfo)
    {
        var chainId = _chainProvider.GetChainId(aelfChainId);
        var queryId = Hash.LoadFromHex(oracleQueryInfo.QueryId);
        var data = await _dataProvider.GetDataAsync(queryId);
        if (string.IsNullOrEmpty(data))
        {
            _logger.LogError("Failed to reveal data for query {Id}",oracleQueryInfo.QueryId);
            return;
        }
        
        _logger.LogInformation("Get data for revealing: {Data}",data);
        var revealInput = new RevealInput
        {
            QueryId = queryId,
            Data = data,
            Salt = _saltProvider.GetSalt(chainId,queryId)
        };
        _logger.LogInformation("Sending Reveal tx with input: {Input}",revealInput);
        var transaction = await _oracleService.RevealAsync(chainId,revealInput);
        _logger.LogInformation("[Reveal] Transaction id :{Id}",transaction.TransactionResult.TransactionId.ToHex());
    }
}