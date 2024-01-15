namespace AElf.Nethereum.Core.Options;

public class TronClientOptions
{
    public List<TronClient> ClientConfigList { get; set; }
}

public class TronClient
{
    public string Alias { get; set; }
    public string Url { get; set; }
    public string ApiKey { get; set; }
}