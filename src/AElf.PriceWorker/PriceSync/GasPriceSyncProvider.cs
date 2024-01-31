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
        var setGasPriceInput = new SetGasPriceInput();
        foreach (var item in _priceSyncOptions.SourceChains)
        {
            var gasFee = await _blockchainTransactionFeeService.GetTransactionFeeAsync(item.ChainType);

            if (_priceFluctuationProvider.IsGasPriceFluctuationExceeded(item.ChainId, gasFee.FeeInSmallestUnit))
            {
                setGasPriceInput.GasPriceList.Add(new GasPrice()
                {
                    ChainId = item.ChainId,
                    GasPrice_ = gasFee.FeeInSmallestUnit
                });
            }
        }

        if (setGasPriceInput.GasPriceList.Count == 0)
        {
            return;
        }

        foreach (var item in _priceSyncOptions.TargetChains)
        {
            await _bridgeService.SetGasPriceAsync(item, setGasPriceInput);
            _logger.LogDebug("SetGasPrice success, ChainId: {chainId}", item);
        }

        foreach (var syncGasPrice in setGasPriceInput.GasPriceList)
        {
            _priceFluctuationProvider.SetLatestGasPrice(syncGasPrice.ChainId, syncGasPrice.GasPrice_);
        }
    }
}