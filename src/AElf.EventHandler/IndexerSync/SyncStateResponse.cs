using System.Collections.Generic;

namespace AElf.EventHandler.IndexerSync;

public class SyncStateResponse
{
    public SyncStateItems CurrentVersion { get; set; }
}

public class SyncStateItems
{
    public string Version { get; set; }
    public List<BlockChainStatus> Items { get; set; }
}

public class BlockChainStatus
{
    public string ChainId { get; set; }
    public string LongestChainBlockHash { get; set; }
    public long LongestChainHeight { get; set; }
    public string BestChainBlockHash { get; set; }
    public long BestChainHeight { get; set; }
    public string LastIrreversibleBlockHash { get; set; }
    public long LastIrreversibleBlockHeight { get; set; }
}