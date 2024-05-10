namespace AElf.Nethereum.Core.Options;

public class TronAccountOptions
{
    public string KeyDirectory { get; set; }
    public TronAccountConfig AccountConfig { get; set; } = new();
}

public class TronAccountConfig
{
    public string Alias { get; set; }
    public string Address { get; set; }
    public string Password { get; set; }
    public string PrivateKey { get; set; }
}