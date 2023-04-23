using System;
using System.Collections.Generic;
using AElf.CSharp.Core;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.PriceWorker;

public interface IPriceFluctuationProvider
{
    bool IsGasPriceFluctuationExceeded(Dictionary<string, long> gasPrice);
    bool IsPriceRatioFluctuationExceeded(Dictionary<string, long> priceRatio);
    void SetLatestGasPrice(Dictionary<string, long> gasPrice);
    void SetLatestPriceRatio(Dictionary<string, long> priceRatio);
}

public class PriceFluctuationProvider: IPriceFluctuationProvider,ISingletonDependency
{
    private readonly PriceSyncOptions _priceSyncOptions;
    private Dictionary<string, long> _latestGasPrice;
    private Dictionary<string, long> _latestPriceRatio;

    public PriceFluctuationProvider(IOptionsSnapshot<PriceSyncOptions> priceSyncOptions)
    {
        _latestGasPrice = new Dictionary<string, long>();
        _latestPriceRatio = new Dictionary<string, long>();
        _priceSyncOptions = priceSyncOptions.Value;
    }

    public bool IsGasPriceFluctuationExceeded(Dictionary<string, long> gasPrice)
    {
        foreach (var gas in gasPrice)
        {
            if (!_latestGasPrice.TryGetValue(gas.Key, out var latestGasPrice))
            {
                return true;
            }

            if (Math.Abs(gas.Value - latestGasPrice) / latestGasPrice > _priceSyncOptions.GasPriceFluctuationThreshold)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPriceRatioFluctuationExceeded(Dictionary<string, long> priceRatio)
    {
        foreach (var ratio in priceRatio)
        {
            if (!_latestPriceRatio.TryGetValue(ratio.Key, out var latestPriceRatio))
            {
                return true;
            }

            if (Math.Abs(ratio.Value - latestPriceRatio) / latestPriceRatio >
                _priceSyncOptions.PriceRatioFluctuationThreshold)
            {
                return true;
            }
        }

        return false;
    }

    public void SetLatestGasPrice(Dictionary<string,long> gasPrice)
    {
        _latestGasPrice = gasPrice;
    }
    
    public void SetLatestPriceRatio(Dictionary<string,long> priceRatio)
    {
        _latestPriceRatio = priceRatio;
    }
}