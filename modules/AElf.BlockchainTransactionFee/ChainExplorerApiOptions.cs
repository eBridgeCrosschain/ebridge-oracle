namespace AElf.BlockchainTransactionFee;

public class ChainExplorerApiOptions
{
    public Dictionary<string, string> Url { get; set; } = new(); 
    public Dictionary<string, string> ApiKeys { get; set; } = new();
}