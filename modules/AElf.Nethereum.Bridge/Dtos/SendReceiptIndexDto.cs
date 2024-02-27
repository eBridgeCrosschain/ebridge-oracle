using System.Numerics;

namespace AElf.Nethereum.Bridge.Dtos;

public class SendReceiptIndexDto
{
    public List<BigInteger> Indexes { get; set; }

    public SendReceiptIndexDto()
    {
        Indexes = new List<BigInteger>();
    }
}