using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using AElf.Client.Bridge;
using AElf.Client.Core.Options;
using AElf.Client.MerkleTreeContract;
using AElf.Client.Oracle;
using AElf.Contracts.MerkleTreeContract;
using AElf.Contracts.Oracle;
using AElf.Nethereum.Bridge;
using AElf.Nethereum.Core;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public interface IReceiptProvider
{
    Task ExecuteAsync();
}

public class ReceiptProvider : IReceiptProvider, ITransientDependency
{
    private readonly BridgeOptions _bridgeOptions;
    private readonly IBridgeInService _bridgeInService;
    private readonly INethereumService _nethereumService;
    private readonly IOracleService _oracleService;
    private readonly IBridgeService _bridgeContractService;
    private readonly IMerkleTreeContractService _merkleTreeContractService;
    private readonly ILatestQueriedReceiptCountProvider _latestQueriedReceiptCountProvider;
    private readonly ILogger<ReceiptProvider> _logger;
    private readonly AElfContractOptions _contractOptions;
    private readonly BlockConfirmationOptions _blockConfirmationOptions;
    private const long MaxQueryRange = 100;

    public ReceiptProvider(
        IOptionsSnapshot<BridgeOptions> bridgeOptions,
        IOptionsSnapshot<BlockConfirmationOptions> blockConfirmation,
        IOptionsSnapshot<AElfContractOptions> contractOptions,
        IBridgeInService bridgeInService,
        INethereumService nethereumService,
        IOracleService oracleService,
        IBridgeService bridgeService,
        IMerkleTreeContractService merkleTreeContractService,
        ILatestQueriedReceiptCountProvider latestQueriedReceiptCountProvider,
        ILogger<ReceiptProvider> logger)
    {
        _bridgeOptions = bridgeOptions.Value;
        _bridgeInService = bridgeInService;
        _nethereumService = nethereumService;
        _oracleService = oracleService;
        _bridgeContractService = bridgeService;
        _merkleTreeContractService = merkleTreeContractService;
        _latestQueriedReceiptCountProvider = latestQueriedReceiptCountProvider;
        _logger = logger;
        _contractOptions = contractOptions.Value;
        _blockConfirmationOptions = blockConfirmation.Value;
    }

    public async Task ExecuteAsync()
    {
        var bridgeItemsMap = new Dictionary<(string, string), List<BridgeItemIn>>();
        var tokenIndex = new Dictionary<(string, string), BigInteger>();
        foreach (var bridgeItem in _bridgeOptions.BridgesIn)
        {
            var key = (bridgeItem.ChainId, bridgeItem.EthereumBridgeInContractAddress);
            if (!bridgeItemsMap.TryGetValue(key, out var items))
            {
                items = new List<BridgeItemIn>();
            }

            items.Add(bridgeItem);
            bridgeItemsMap[key] = items;
        }

        foreach (var (aliasAddress, item) in bridgeItemsMap)
        {
            var tokenList = item.Select(i => i.OriginToken).ToList();
            var targetChainIdList = item.Select(i => i.TargetChainId).ToList();
            var tokenAndChainIdList = item.Select(i => (i.TargetChainId, i.OriginToken)).ToList();
            _logger.LogInformation(
                "Start to get transfer receipt from ethereum. From chainId:{ChainId},ethereum bridgeIn contract address:{Address}",
                aliasAddress.Item1, aliasAddress.Item2);
            var sendReceiptIndexDto = await _bridgeInService.GetTransferReceiptIndexAsync(aliasAddress.Item1,
                aliasAddress.Item2, tokenList, targetChainIdList);
            var list = tokenList.Select(async (_,i) =>
            {
                _logger.LogInformation(
                    "Transfer token:{Token}, target chain id:{ChainId}, token index:{Index}",
                    tokenAndChainIdList[i].OriginToken,
                    tokenAndChainIdList[i].TargetChainId, sendReceiptIndexDto.Indexes[i]);
                tokenIndex[tokenAndChainIdList[i]] = sendReceiptIndexDto.Indexes[i];
                var targetChainId = _bridgeOptions.BridgesIn.Single(j => j.SwapId == item[i].SwapId).TargetChainId;
                await SendQueryAsync(targetChainId, item[i], tokenIndex[(item[i].TargetChainId, item[i].OriginToken)]);
            });
            await Task.WhenAll(list);
        }
    }

    private async Task SendQueryAsync(string chainId, BridgeItemIn bridgeItem, BigInteger tokenIndex)
    {
        var swapId = bridgeItem.SwapId;
        var isPaused = await _bridgeContractService.IsContractPause(chainId);
        if (isPaused.Value)
        {
            return;
        }

        var spaceId = await _bridgeContractService.GetSpaceIdBySwapIdAsync(chainId, Hash.LoadFromHex(swapId));
        var lastRecordedLeafIndex = (await _merkleTreeContractService.GetLastLeafIndexAsync(
            chainId, new GetLastLeafIndexInput
            {
                SpaceId = spaceId
            })).Value;
        if (lastRecordedLeafIndex == -1)
        {
            _logger.LogInformation("Space of id {Id} is not created", spaceId);
            return;
        }

        var nextTokenIndex = lastRecordedLeafIndex == -2 ? 1 : lastRecordedLeafIndex + 2;
        if (_latestQueriedReceiptCountProvider.Get(swapId) == 0)
        {
            _latestQueriedReceiptCountProvider.Set(DateTime.UtcNow, swapId, nextTokenIndex);
        }
        else if (_latestQueriedReceiptCountProvider.Get(swapId) != nextTokenIndex)
        {
            var receiptIndexNow = _latestQueriedReceiptCountProvider.Get(swapId);
            _logger.LogInformation(
                "Latest queried receipt index : {Index}, Last recorded leaf index : {LastIndex}, Wait", receiptIndexNow,
                nextTokenIndex);
            return;
        }

        var nextRoundStartTokenIndex = _latestQueriedReceiptCountProvider.Get(swapId);
        _logger.LogInformation(
            "{ChainId}-{TargetId}-{Token},Last recorded leaf index : {Index}. Next round to query should begin with receipt Index:{TokenIndex}",
            bridgeItem.ChainId, bridgeItem.TargetChainId, bridgeItem.OriginToken, lastRecordedLeafIndex,
            nextRoundStartTokenIndex);

        if (tokenIndex < nextRoundStartTokenIndex)
        {
            return;
        }

        tokenIndex = tokenIndex - nextRoundStartTokenIndex + 1 > MaxQueryRange
            ? nextRoundStartTokenIndex + MaxQueryRange - 1
            : tokenIndex;
        var notRecordTokenNumber = tokenIndex - nextRoundStartTokenIndex + 1;
        if (notRecordTokenNumber <= 0) return;

        var blockNumber = await _nethereumService.GetBlockNumberAsync(bridgeItem.ChainId);
        var getReceiptInfos = await _bridgeInService.GetSendReceiptInfosAsync(bridgeItem.ChainId,
            bridgeItem.EthereumBridgeInContractAddress, bridgeItem.OriginToken, bridgeItem.TargetChainId,
            nextRoundStartTokenIndex, (long) tokenIndex);
        var lastTokenIndexConfirm = nextRoundStartTokenIndex - 1;
        string receiptIdHash = null;
        for (var i = 0; i < notRecordTokenNumber; i++)
        {
            var blockHeight = getReceiptInfos.Receipts[i].BlockHeight;
            receiptIdHash = getReceiptInfos.Receipts[i].ReceiptId.Split(".").First();
            var blockConfirmationCount = _blockConfirmationOptions.ConfirmationCount[bridgeItem.ChainId];
            if (blockNumber - blockHeight > blockConfirmationCount)
            {
                lastTokenIndexConfirm = (i + nextRoundStartTokenIndex);
                continue;
            }

            break;
        }

        _logger.LogInformation(
            "{ChainId}-{TargetId}-{Token}.Token hash in receipt id:{Id},Last confirmed receipt index:{Index}",
            bridgeItem.ChainId,
            bridgeItem.TargetChainId, bridgeItem.OriginToken, receiptIdHash, lastTokenIndexConfirm);

        if (lastTokenIndexConfirm - nextRoundStartTokenIndex < 0) return;

        _logger.LogInformation(
            "{ChainId}-{TargetId}-{Token}.Start to query token : from receipt index {Index},end receipt index {EndIndex}",
            bridgeItem.ChainId,
            bridgeItem.TargetChainId, bridgeItem.OriginToken, nextRoundStartTokenIndex, lastTokenIndexConfirm);

        await SendQueryOracleAsync(swapId, chainId, receiptIdHash, nextRoundStartTokenIndex, lastTokenIndexConfirm,
            bridgeItem.QueryToAddress);
        
    }

    private async Task SendQueryOracleAsync(string swapId, string chainId, string receiptIdHash,
        long nextRoundStartTokenIndex, long lastTokenIndexConfirm, string queryToAddress)
    {
        var queryInput = new QueryInput
        {
            Payment = _bridgeOptions.QueryPayment,
            QueryInfo = new QueryInfo
            {
                Title = $"record_receipts_{swapId}",
                Options =
                {
                    $"{receiptIdHash}.{nextRoundStartTokenIndex}", $"{receiptIdHash}.{lastTokenIndexConfirm}"
                }
            },
            AggregatorContractAddress =
                _contractOptions.ContractAddressList[chainId]["StringAggregatorContract"].ConvertAddress(),
            CallbackInfo = new CallbackInfo
            {
                ContractAddress =
                    _contractOptions.ContractAddressList[chainId]["BridgeContract"].ConvertAddress()
            },
            DesignatedNodeList = new AddressList
            {
                Value = {queryToAddress.ConvertAddress()}
            }
        };
        _latestQueriedReceiptCountProvider.Set(DateTime.UtcNow, swapId, lastTokenIndexConfirm + 1);
        var sendTxResult = await _oracleService.QueryAsync(chainId, queryInput);
        _logger.LogInformation(
            "Query transaction id : {Id}.Next receipt index should start with: {Index}",
            sendTxResult.TransactionResult.TransactionId.ToHex(), _latestQueriedReceiptCountProvider.Get(swapId));
    }
}