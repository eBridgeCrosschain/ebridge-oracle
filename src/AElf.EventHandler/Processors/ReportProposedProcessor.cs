using System.Threading.Tasks;
using AElf.Client.Core;
using AElf.Client.Core.Options;
using AElf.Client.Report;
using AElf.Contracts.Report;
using AElf.EventHandler.IndexerSync;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public interface IReportProposedProcessor
{
    Task ProcessAsync(string aelfChainId, ReportInfoDto reportQueryInfo);
}

public class ReportProposedProcessor : IReportProposedProcessor,ITransientDependency
{
    private readonly IReportService _reportService;
    private readonly IAElfAccountProvider _accountProvider;
    private readonly AElfClientConfigOptions _aelfClientConfigOptions;
    private readonly IChainProvider _chainProvider;
    private readonly IReportProvider _reportProvider;

    private readonly ILogger<ReportProposedProcessor> _logger;

    public ReportProposedProcessor(
        IReportService reportService,
        IAElfAccountProvider accountProvider,
        ILogger<ReportProposedProcessor> logger,
        IOptionsSnapshot<AElfClientConfigOptions> aelfConfigOptions, IChainProvider chainProvider, IReportProvider reportProvider)
    {
        _logger = logger;
        _chainProvider = chainProvider;
        _reportProvider = reportProvider;
        _reportService = reportService;
        _accountProvider = accountProvider;
        _aelfClientConfigOptions = aelfConfigOptions.Value;
    }

    public async Task ProcessAsync(string aelfChainId, ReportInfoDto reportQueryInfo)
    {
        _logger.LogInformation("[ReportProposed] Receive report event.");
        //TODO:Check permission
        var chainId = _chainProvider.GetChainId(aelfChainId);
        var privateKey = _accountProvider.GetPrivateKey(_aelfClientConfigOptions.AccountAlias);
        var res = await _reportProvider.ValidateReportAsync(chainId, reportQueryInfo.Token, reportQueryInfo.RoundId,
            reportQueryInfo.QueryInfo);
        if (res)
        {
            _logger.LogInformation("Validate report success.{chainId}-{token}-{roundId}",chainId,reportQueryInfo.Token, reportQueryInfo.RoundId);
            var sendTxResult = await _reportService.ConfirmReportAsync(chainId,new ConfirmReportInput
            {
                ChainId = reportQueryInfo.TargetChainId,
                Token = reportQueryInfo.Token,
                RoundId = reportQueryInfo.RoundId,
                Signature = SignHelper
                    .GetSignature(reportQueryInfo.RawReport, privateKey).RecoverInfo
            });
            _logger.LogInformation("[ConfirmReport] Transaction id :{Id}",sendTxResult.TransactionResult.TransactionId.ToHex());
        }
        else
        {
            _logger.LogWarning("Validate report failed.{chainId}-{token}-{roundId}",chainId,reportQueryInfo.Token, reportQueryInfo.RoundId);
        }
    }
}