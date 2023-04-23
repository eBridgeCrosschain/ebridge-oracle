using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.BlockchainTransactionFee;
using AElf.Client.Bridge;
using AElf.Client.Core;
using AElf.Contracts.Bridge;
using AElf.Nethereum.Core;
using AElf.TokenPrice;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElf.PriceWorker;

public class PriceSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly PriceSyncOptions _priceSyncOptions;
    private readonly IBridgeService _bridgeService;
    private readonly IBlockchainTransactionFeeService _blockchainTransactionFeeService;
    private readonly ITokenPriceService _tokenPriceService;
    private readonly IPriceFluctuationProvider _priceFluctuationProvider;

    public PriceSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<PriceSyncOptions> priceSyncOptions, IBridgeService bridgeService,
        IBlockchainTransactionFeeService blockchainTransactionFeeService, ITokenPriceService tokenPriceService,
        IPriceFluctuationProvider priceFluctuationProvider) : base(
        timer, serviceScopeFactory)
    {
        _bridgeService = bridgeService;
        _blockchainTransactionFeeService = blockchainTransactionFeeService;
        _tokenPriceService = tokenPriceService;
        _priceFluctuationProvider = priceFluctuationProvider;
        _priceSyncOptions = priceSyncOptions.Value;

        Timer.Period = 1000 * _priceSyncOptions.SyncInterval;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var setGasPriceInput = new SetGasPriceInput();
        var setPriceRatioInput = new SetPriceRatioInput();
        var elfPrice = await _tokenPriceService.GetPriceAsync("ELF");

        foreach (var item in _priceSyncOptions.SourceChains)
        {
            var gasFee = await _blockchainTransactionFeeService.GetTransactionFeeAsync(item.ChainType);
            var feeWei = (long)(gasFee.Fee * (decimal)Math.Pow(10, 9));
            setGasPriceInput.GasPriceList.Add(new GasPrice
            {
                ChainId = item.ChainId,
                GasPrice_ = feeWei
            });
        
            var nativePrice = await _tokenPriceService.GetPriceAsync(item.NativeToken);
            var ratio = (long)(nativePrice * (decimal)Math.Pow(10, 8) / elfPrice);
            setPriceRatioInput.Value.Add(new PriceRatio
            {
                TargetChainId = item.ChainId,
                PriceRatio_ = ratio
            });
        }

        await SetGasPriceAsync(setGasPriceInput);
        await SetPriceRatioAsync(setPriceRatioInput);
    }
    
    private async Task SetGasPriceAsync(SetGasPriceInput setGasPriceInput)
    {
        var gasPrice = setGasPriceInput.GasPriceList.ToDictionary(o=>o.ChainId,o=>o.GasPrice_);
        if (_priceFluctuationProvider.IsGasPriceFluctuationExceeded(gasPrice))
        {
            foreach (var item in _priceSyncOptions.TargetChains)
            {
                await _bridgeService.SetGasPriceAsync(item, setGasPriceInput);
                Logger.LogDebug("SetGasPrice success, ChainId: {Item}", item);
            }
            _priceFluctuationProvider.SetLatestGasPrice(gasPrice);
        }
    }

    private async Task SetPriceRatioAsync(SetPriceRatioInput setPriceRatioInput)
    {
        var priceRatio = setPriceRatioInput.Value.ToDictionary(o => o.TargetChainId, o => o.PriceRatio_);
        if (_priceFluctuationProvider.IsPriceRatioFluctuationExceeded(priceRatio))
        {
            foreach (var item in _priceSyncOptions.TargetChains)
            {
                await _bridgeService.SetPriceRatioAsync(item, setPriceRatioInput);
                Logger.LogDebug("SetPriceRatio success, ChainId: {Item}", item);
            }

            _priceFluctuationProvider.SetLatestPriceRatio(priceRatio);
        }
    }
}