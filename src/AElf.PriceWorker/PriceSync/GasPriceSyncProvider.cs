using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.BlockchainTransactionFee;
using AElf.Client.Bridge;
using AElf.Contracts.Bridge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.PriceWorker.PriceSync;

public class GasPriceSyncProvider : IPriceSyncProvider
{
    private readonly PriceSyncOptions _priceSyncOptions;
    private readonly IBridgeService _bridgeService;
    private readonly IPriceFluctuationProvider _priceFluctuationProvider;
    private readonly IBlockchainTransactionFeeService _blockchainTransactionFeeService;
    private readonly ILogger<GasPriceSyncProvider> _logger;

    public GasPriceSyncProvider(IOptionsSnapshot<PriceSyncOptions> priceSyncOptions, IBridgeService bridgeService,
        IPriceFluctuationProvider priceFluctuationProvider,
        IBlockchainTransactionFeeService blockchainTransactionFeeService, ILogger<GasPriceSyncProvider> logger)
    {
        _priceSyncOptions = priceSyncOptions.Value;
        _bridgeService = bridgeService;
        _priceFluctuationProvider = priceFluctuationProvider;
        _blockchainTransactionFeeService = blockchainTransactionFeeService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogDebug("Start to set gas price.");
        var setGasPriceInput = new SetGasPriceInput();
        foreach (var item in _priceSyncOptions.SourceChains)
        {
            if (item.ChainType == "TON")
            {
                return;
            }
            _logger.LogDebug("Start to set gas price，chain type:{type}.", item.ChainType);
            var gasFee = await _blockchainTransactionFeeService.GetTransactionFeeAsync(item.ChainType);
            _logger.LogDebug("Get gas fee success, ChainId: {chainId}, Fee: {fee}", item.ChainId, gasFee.Fee);
            var feeWei = (long)(gasFee.Fee * (decimal)Math.Pow(10, 9));

            if (_priceFluctuationProvider.IsGasPriceFluctuationExceeded(item.ChainId, feeWei))
            {
                _logger.LogDebug("Gas price fluctuation exceeded, ChainId: {chainId}, Fee: {fee}", item.ChainId, feeWei);
                setGasPriceInput.GasPriceList.Add(new GasPrice()
                {
                    ChainId = item.ChainId,
                    GasPrice_ = feeWei
                });
            }
        }

        if (setGasPriceInput.GasPriceList.Count == 0)
        {
            _logger.LogDebug("No gas price fluctuation exceeded.");
            return;
        }
        _logger.LogDebug("Gas price fluctuation exceeded, start to set gas price.");
        foreach (var item in _priceSyncOptions.TargetChains)
        {
            _logger.LogDebug("Start to set gas price, ChainId: {chainId}", item);
            await _bridgeService.SetGasPriceAsync(item, setGasPriceInput);
            _logger.LogDebug("SetGasPrice success, ChainId: {chainId}", item);
        }

        foreach (var syncGasPrice in setGasPriceInput.GasPriceList)
        {
            _priceFluctuationProvider.SetLatestGasPrice(syncGasPrice.ChainId, syncGasPrice.GasPrice_);
        }
    }
}