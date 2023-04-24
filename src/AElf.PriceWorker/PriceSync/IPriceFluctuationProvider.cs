using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.PriceWorker.PriceSync;

public interface IPriceFluctuationProvider
{
    bool IsGasPriceFluctuationExceeded(string chainId, long gasPrice);
    bool IsPriceRatioFluctuationExceeded(string symbol, long priceRatio);
    void SetLatestGasPrice(string chainId, long gasPrice);
    void SetLatestPriceRatio(string symbol, long priceRatio);
}

public class PriceFluctuationProvider: IPriceFluctuationProvider,ISingletonDependency
{
    private readonly PriceSyncOptions _priceSyncOptions;
    private readonly Dictionary<string, long> _latestGasPrice;
    private readonly Dictionary<string, long> _latestPriceRatio;
    private readonly ILogger<PriceFluctuationProvider> _logger;

    public PriceFluctuationProvider(IOptionsSnapshot<PriceSyncOptions> priceSyncOptions,
        ILogger<PriceFluctuationProvider> logger)
    {
        _logger = logger;
        _latestGasPrice = new Dictionary<string, long>();
        _latestPriceRatio = new Dictionary<string, long>();
        _priceSyncOptions = priceSyncOptions.Value;
    }

    public bool IsGasPriceFluctuationExceeded(string chainId, long gasPrice)
    {
        if (!_latestGasPrice.TryGetValue(chainId, out var latestGasPrice))
        {
            return true;
        }

        _priceSyncOptions.GasPriceFluctuationThreshold.TryGetValue(chainId, out var threshold);
        var fluctuation = Math.Abs(gasPrice - latestGasPrice) / (float)latestGasPrice;
        if (fluctuation > threshold)
        {
            return true;
        }

        _logger.LogDebug(
            "Gas price fluctuation is not exceeded. ChainId: {ChainId}, GasPrice: {GasPrice}, LatestGasPrice: {LatestGasPrice}, Fluctuation: {Fluctuation}, Threshold: {Threshold}",
            chainId, gasPrice, latestGasPrice, fluctuation, threshold);
        return false;
    }

    public bool IsPriceRatioFluctuationExceeded(string symbol, long priceRatio)
    {
        if (!_latestPriceRatio.TryGetValue(symbol, out var latestPriceRatio))
        {
            return true;
        }

        _priceSyncOptions.PriceRatioFluctuationThreshold.TryGetValue(symbol, out var threshold);
        var fluctuation = Math.Abs(priceRatio - latestPriceRatio) / (float)latestPriceRatio;
        if (fluctuation > threshold)
        {
            return true;
        }
        
        _logger.LogDebug(
            "Price ratio fluctuation is not exceeded. Symbol: {Symbol}, PriceRatio: {PriceRatio}, LatestPriceRatio: {LatestPriceRatio}, Fluctuation: {Fluctuation}, Threshold: {Threshold}",
            symbol, priceRatio, latestPriceRatio, fluctuation, threshold);
        return false;
    }

    public void SetLatestGasPrice(string chainId, long gasPrice)
    {
        _latestGasPrice[chainId] = gasPrice;
    }

    public void SetLatestPriceRatio(string symbol, long priceRatio)
    {
        _latestPriceRatio[symbol] = priceRatio;
    }
}