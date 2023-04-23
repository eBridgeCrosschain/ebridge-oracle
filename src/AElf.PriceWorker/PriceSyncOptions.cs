using System.Collections.Generic;

namespace AElf.PriceWorker;

public class PriceSyncOptions
{
    public List<ChainItem> SourceChains { get; set; } = new();
    public List<string> TargetChains { get; set; } = new();

    public int SyncInterval { get; set; } = 60 * 1000;
}

public class ChainItem
{
    public string ChainId { get; set; }
    public string ChainType { get; set; }
    public string NativeToken { get; set; }
}