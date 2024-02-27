using System.Collections.Generic;

namespace AElf.EventHandler;

public class IndexerSyncOptions
{
    public Dictionary<string, long> StartSyncHeight { get; set; } = new();
}