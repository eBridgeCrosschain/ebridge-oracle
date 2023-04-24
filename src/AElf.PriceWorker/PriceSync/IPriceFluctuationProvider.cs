using System;
using System.Collections.Generic;
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

    public PriceFluctuationProvider(IOptionsSnapshot<PriceSyncOptions> priceSyncOptions)
    {
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
        if (Math.Abs(gasPrice - latestGasPrice) / latestGasPrice > threshold)
        {
            return true;
        }

        return false;
    }

    public bool IsPriceRatioFluctuationExceeded(string symbol, long priceRatio)
    {
        if (!_latestPriceRatio.TryGetValue(symbol, out var latestPriceRatio))
        {
            return true;
        }

        _priceSyncOptions.PriceRatioFluctuationThreshold.TryGetValue(symbol, out var threshold);
        if (Math.Abs(priceRatio - latestPriceRatio) / latestPriceRatio > threshold)
        {
            return true;
        }

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