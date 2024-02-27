using System.Numerics;

namespace AElf.Nethereum.Bridge.Dtos;

public class ReceiptInfosDto
{
    public List<ReceiptDto> Receipts { get; set; }

    public ReceiptInfosDto()
    {
        Receipts = new List<ReceiptDto>();
    }
}

public class ReceiptDto
{
    public string Asset { get; set; }
    public string Owner { get; set; }
    public BigInteger Amount { get; set; }
    public BigInteger BlockHeight { get; set; }
    public BigInteger BlockTime { get; set; }
    public string TargetChainId { get; set; }
    public string TargetAddress { get; set; }
    public string ReceiptId { get; set; }
}