using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Client.Bridge;
using AElf.Contracts.Bridge;
using AElf.TokenPrice;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.PriceWorker.PriceSync;

public class PriceRatioProvider : IPriceSyncProvider, ITransientDependency
{
    private readonly PriceSyncOptions _priceSyncOptions;
    private readonly IBridgeService _bridgeService;
    private readonly ITokenPriceService _tokenPriceService;
    private readonly IPriceFluctuationProvider _priceFluctuationProvider;
    private readonly ILogger<PriceRatioProvider> _logger;

    public PriceRatioProvider(IOptionsSnapshot<PriceSyncOptions> priceSyncOptions, IBridgeService bridgeService,
        ITokenPriceService tokenPriceService, IPriceFluctuationProvider priceFluctuationProvider,
        ILogger<PriceRatioProvider> logger)
    {
        _priceSyncOptions = priceSyncOptions.Value;
        _bridgeService = bridgeService;
        _tokenPriceService = tokenPriceService;
        _priceFluctuationProvider = priceFluctuationProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var elfPrice = await _tokenPriceService.GetPriceAsync("ELF");

        var toSyncPriceRatio = new List<SyncPriceRatio>();
        foreach (var item in _priceSyncOptions.SourceChains)
        {
            var nativePrice = await _tokenPriceService.GetPriceAsync(item.NativeToken);
            var ratio = (long)(nativePrice * (decimal)Math.Pow(10, 8) / elfPrice);

            if (_priceFluctuationProvider.IsPriceRatioFluctuationExceeded(item.NativeToken, ratio))
            {
                toSyncPriceRatio.Add(new SyncPriceRatio
                {
                    ChainId = item.ChainId,
                    Symbol = item.NativeToken,
                    PriceRatio = ratio
                });
            }
        }

        if (toSyncPriceRatio.Count == 0)
        {
            return;
        }

        var setPriceRatioInput = new SetPriceRatioInput();
        foreach (var syncPriceRatio in toSyncPriceRatio)
        {
            setPriceRatioInput.Value.Add(new PriceRatio
            {
                TargetChainId = syncPriceRatio.ChainId,
                PriceRatio_ = syncPriceRatio.PriceRatio
            });
        }

        foreach (var item in _priceSyncOptions.TargetChains)
        {
            await _bridgeService.SetPriceRatioAsync(item, setPriceRatioInput);
            _logger.LogDebug("SetPriceRatio success, ChainId: {chainId}", item);
        }

        foreach (var syncPriceRatio in toSyncPriceRatio)
        {
            _priceFluctuationProvider.SetLatestPriceRatio(syncPriceRatio.Symbol, syncPriceRatio.PriceRatio);
        }
    }
}

public class SyncPriceRatio
{
    public string ChainId { get; set; }
    public string Symbol { get; set; }
    public long PriceRatio { get; set; }
}