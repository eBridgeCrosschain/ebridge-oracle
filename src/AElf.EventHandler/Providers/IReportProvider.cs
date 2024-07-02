using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Client.Bridge;
using AElf.Contracts.Bridge;
using AElf.EventHandler.IndexerSync;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler
{
    public interface IReportProvider
    {
        Task<bool> ValidateReportAsync(string chainId, string ethereumContractAddress, long roundId,
            OffChainQueryInfoDto queryInfo);

        void SetReport(string ethereumContractAddress, long roundId, string report);
        string GetReport(string ethereumContractAddress, long roundId);
        void RemoveReport(string ethereumContractAddress, long roundId);
    }

    public class ReportProvider : IReportProvider, ISingletonDependency
    {
        private readonly Dictionary<string, Dictionary<long, string>> _reportDictionary;
        private ILogger<ReportProvider> _logger;
        private readonly IBridgeService _bridgeService;

        public ReportProvider(ILogger<ReportProvider> logger, IBridgeService bridgeService)
        {
            _reportDictionary = new Dictionary<string, Dictionary<long, string>>();
            _logger = logger;
            _bridgeService = bridgeService;
        }

        public async Task<bool> ValidateReportAsync(string chainId, string ethereumContractAddress, long roundId,
            OffChainQueryInfoDto queryInfo)
        {
            var title = queryInfo.Title;
            var options = queryInfo.Options;
            if (title == null || options == null)
            {
                _logger.LogError("No data of for report.{contract},{roundId}", ethereumContractAddress, roundId);
                return false;
            }

            if (title.StartsWith("lock_token"))
            {
                _logger.LogInformation("Start to validate report.{chainId}-{token}-{roundId}", chainId,
                    ethereumContractAddress, roundId);
                var receiptId = title.Split("_").Last();
                var receiptInfo = options.Last();
                var res = long.TryParse(receiptInfo.Split("-").First(), out var amount);
                if (!res)
                {
                    _logger.LogError("Failed to parse amount.{contract},{roundId}", ethereumContractAddress, roundId);
                    return false;
                }
                var targetAddress = receiptInfo.Split("-")[1];
                _logger.LogInformation("Receipt from event.{chainId}-{token}-{roundId}-{amount}-{targetAddress}",
                    chainId, ethereumContractAddress, roundId, amount, targetAddress);
                var receipt = await _bridgeService.GetReceiptInfoAsync(chainId, new StringValue
                {
                    Value = receiptId
                });
                _logger.LogInformation("Receipt from chain.{receipt}", JsonConvert.SerializeObject(receipt));
                return !receipt.Equals(new Receipt()) && receipt.Symbol != null && receipt.Amount == amount &&
                       receipt.TargetAddress.Equals(targetAddress);
            }

            return false;
        }

        public void SetReport(string ethereumContractAddress, long roundId, string report)
        {
            if (!_reportDictionary.TryGetValue(ethereumContractAddress, out var roundReport))
            {
                roundReport = new Dictionary<long, string>();
                _reportDictionary[ethereumContractAddress] = roundReport;
            }

            if (!roundReport.ContainsKey(roundId))
                roundReport[roundId] = report;
        }

        public string GetReport(string ethereumContractAddress, long roundId)
        {
            if (!_reportDictionary.TryGetValue(ethereumContractAddress, out var roundReport))
            {
                _logger.LogInformation("Address: {Address} report dose not exist", ethereumContractAddress);
                return string.Empty;
            }

            if (roundReport.TryGetValue(roundId, out var report)) return report;
            _logger.LogInformation("Address: {Address} RoundId: {RoundId} report dose not exist",
                ethereumContractAddress, roundId);
            return string.Empty;
        }

        public void RemoveReport(string ethereumContractAddress, long roundId)
        {
            if (!_reportDictionary.TryGetValue(ethereumContractAddress, out var roundReport))
                return;
            if (!roundReport.TryGetValue(roundId, out _))
                return;
            roundReport.Remove(roundId);
            if (roundReport.Count == 0)
                _reportDictionary.Remove(ethereumContractAddress);
        }
    }
}