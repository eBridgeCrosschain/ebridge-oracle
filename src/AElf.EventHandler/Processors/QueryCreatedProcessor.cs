using System.Linq;
using System.Threading.Tasks;
using AElf.Client.Core.Extensions;
using AElf.Client.Core.Options;
using AElf.Client.Oracle;
using AElf.Contracts.Oracle;
using AElf.EventHandler.IndexerSync;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public interface IQueryCreatedProcessor
{
    Task ProcessAsync(string aelfChainId, OracleQueryInfoDto oracleQueryInfo);
}

internal class QueryCreatedProcessor : IQueryCreatedProcessor,ITransientDependency
{
    private readonly ISaltProvider _saltProvider;
    private readonly IDataProvider _dataProvider;
    private readonly BridgeOptions _bridgeOptions;
    private readonly OracleOptions _oracleOptions;
    private readonly IOracleService _oracleService;
    private readonly IChainIdProvider _chainIdProvider;

    private readonly ILogger<QueryCreatedProcessor> _logger;

    public QueryCreatedProcessor(
        ISaltProvider saltProvider, 
        IDataProvider dataProvider, 
        ILogger<QueryCreatedProcessor> logger,
        IOptionsSnapshot<BridgeOptions> bridgeOptions,
        IOptionsSnapshot<OracleOptions> oracleOptions,
        IOracleService oracleService, IChainIdProvider chainIdProvider)
    {
        _saltProvider = saltProvider;
        _dataProvider = dataProvider;
        _logger = logger;
        _bridgeOptions = bridgeOptions.Value;
        _oracleOptions = oracleOptions.Value;
        _oracleService = oracleService;
        _chainIdProvider = chainIdProvider;
    }

    public async Task ProcessAsync(string aelfChainId, OracleQueryInfoDto oracleQueryInfo)
    {
        var chainId = _chainIdProvider.GetChainId(aelfChainId);
        
        var firstDesignatedNodeAddress = oracleQueryInfo.DesignatedNodeList.First();
        //var queryToken = queryCreated.Token; // Query token means the ethereum contract address oracle node should cares in report case.
        if (oracleQueryInfo.DesignatedNodeList.Contains(_bridgeOptions.AccountAddress) ||
            _oracleOptions.ObserverAssociationAddressList.Contains(firstDesignatedNodeAddress))
        {
            var queryId = Hash.LoadFromHex(oracleQueryInfo.QueryId);
            var data = await _dataProvider.GetDataAsync(queryId, oracleQueryInfo.QueryInfo.Title,
                oracleQueryInfo.QueryInfo.Options.ToList());
            if (string.IsNullOrEmpty(data))
            {
                var swapId = oracleQueryInfo.QueryInfo.Title.Split("_").Last();
                _logger.LogError(oracleQueryInfo.QueryInfo.Title == "record_receipts"
                    ? $"Failed to record receipts. Swap Id :{swapId}"
                    : $"Failed to response to query {oracleQueryInfo.QueryId}.");
        
                return;
            }
        
            var salt = _saltProvider.GetSalt(chainId, queryId);
            _logger.LogInformation($"Queried data: {data}, salt: {salt}");
            var commitInput = new CommitInput
            {
                QueryId = queryId,
                Commitment = HashHelper.ConcatAndCompute(
                    HashHelper.ComputeFrom(data),
                    HashHelper.ConcatAndCompute(salt, HashHelper.ComputeFrom(_bridgeOptions.AccountAddress)))
            };
            _logger.LogInformation($"Sending Commit tx with input: {commitInput}");
            var transactionResult = await _oracleService.CommitAsync(chainId, commitInput);
            _logger.LogInformation($"[Commit] Transaction id {transactionResult.TransactionResult.TransactionId}");
        }
    }
}