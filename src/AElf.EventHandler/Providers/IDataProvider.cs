using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AElf.Client.Bridge;
using AElf.Client.Core.Options;
using AElf.Contracts.Bridge;
using AElf.Nethereum.Bridge;
using AElf.Nethereum.Core;
using AElf.Nethereum.Core.Options;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Core;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public interface IDataProvider
{
    Task<string> GetDataAsync(Hash queryId, string title = null, List<string> options = null);
}

public class DataProvider : IDataProvider, ISingletonDependency
{
    private readonly Dictionary<Hash, string> _dictionary;
    private readonly ILogger<DataProvider> _logger;
    private readonly BridgeOptions _bridgeOptions;
    private readonly IBridgeInService _bridgeInService;

    public DataProvider(
        ILogger<DataProvider> logger,
        IOptionsSnapshot<BridgeOptions> bridgeOptions,
        IBridgeInService bridgeInService)
    {
        _logger = logger;
        _bridgeOptions = bridgeOptions.Value;
        _bridgeInService = bridgeInService;
        _dictionary = new Dictionary<Hash, string>();
    }

    public async Task<string> GetDataAsync(Hash queryId, string title = null, List<string> options = null)
    {
        if (title == "invalid")
        {
            return "0";
        }

        if (_dictionary.TryGetValue(queryId, out var data))
        {
            return data;
        }

        if (title == null || options == null)
        {
            _logger.LogError("No data of {Id} for revealing", queryId);
            return string.Empty;
        }

        if (title.StartsWith("record_receipts") && options.Count == 2)
        {
            var swapId = title.Split('_').Last();
            _logger.LogInformation("swapId {Id}", swapId);
            var bridgeItem = _bridgeOptions.BridgesIn.Single(c => c.SwapId == swapId);
            var recordReceiptHashInput =
                await GetReceiptHashMap(Hash.LoadFromHex(swapId), bridgeItem, long.Parse(options[0].Split(".").Last()),
                    long.Parse(options[1].Split(".").Last()));
            _logger.LogInformation(
                "Trying to query record receipt data. Swap id: {Id},About to handle record receipt hashes for swapping tokens,RecordReceiptHashInput: {Input}",
                swapId, recordReceiptHashInput);
            _dictionary[queryId] = recordReceiptHashInput;
            return recordReceiptHashInput;
        }

        return string.Empty;
    }

    private async Task<string> GetReceiptHashMap(Hash swapId, BridgeItemIn bridgeItem, long start, long end)
    {
        var token = _bridgeOptions.BridgesIn.Single(c => c.SwapId == swapId.ToHex()).OriginToken;
        var chainId = _bridgeOptions.BridgesIn.Single(c => c.SwapId == swapId.ToHex()).TargetChainId;
        _logger.LogInformation("swapId {Id},start index {Start},end index {End}", swapId.ToHex(),start, end);
        var receiptInfos = await _bridgeInService.GetSendReceiptInfosAsync(bridgeItem.ChainId,
            bridgeItem.EthereumBridgeInContractAddress, token, chainId, start, end);
        var receiptHashes = new List<Hash>();
        for (var i = 0; i <= end - start; i++)
        {
            var amountHash = HashHelper.ComputeFrom((receiptInfos.Receipts[i].Amount).ToString());
            var targetAddressHash = HashHelper.ComputeFrom(receiptInfos.Receipts[i].TargetAddress);
            var receiptIdHash = HashHelper.ComputeFrom(receiptInfos.Receipts[i].ReceiptId);
            var hash = HashHelper.ConcatAndCompute(amountHash, targetAddressHash, receiptIdHash);
            receiptHashes.Add(hash);
        }

        var input = new ReceiptHashMap
        {
            SwapId = swapId.ToHex()
        };
        _logger.LogInformation("swapId {Id},start index {Start},end index {End}", swapId.ToHex(),start, end);
        for (var i = 0; i <= end - start; i++)
        {
            try
            {
                input.Value.Add(receiptInfos.Receipts[i].ReceiptId, receiptHashes[i].ToHex());
            }
            catch (Exception e)
            {
                _logger.LogInformation("swapId:{SwapId},Receipt id: {Id},message :{e}", swapId.ToHex(),receiptInfos.Receipts[i].ReceiptId,e.Message);
                throw;
            }
            
        }

        return input.ToString();
    }
}