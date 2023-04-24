using System.Collections.Generic;

namespace AElf.PriceWorker.PriceSync;

public class PriceSyncOptions
{
    public List<ChainItem> SourceChains { get; set; } = new();
    public List<string> TargetChains { get; set; } = new();
    public int SyncInterval { get; set; } = 60 * 60;
    public Dictionary<string, float> PriceRatioFluctuationThreshold { get; set; } = new();
    public Dictionary<string, float> GasPriceFluctuationThreshold { get; set; } = new();
}

public class ChainItem
{
    public string ChainId { get; set; }
    public string ChainType { get; set; }
    public string NativeToken { get; set; }
}