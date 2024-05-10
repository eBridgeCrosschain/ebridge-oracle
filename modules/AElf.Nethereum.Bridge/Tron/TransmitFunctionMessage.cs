using Nethereum.Contracts;
using Attributes = Nethereum.ABI.FunctionEncoding.Attributes;

namespace AElf.Nethereum.Bridge.Tron;

[Attributes.Function("transmit", "tuple[]")]
public class TransmitFunctionMessage: FunctionMessage
{
    [Attributes.Parameter("bytes32", "swapHashId", 1)]
    public byte[] SwapHashId { get; set; }
    [Attributes.Parameter("bytes", "_report", 2)]
    public byte[] Report { get; set; }
    [Attributes.Parameter("bytes32[]", "_rs", 3)]
    public byte[][] Rs { get; set; }
    [Attributes.Parameter("bytes32[]", "_ss", 4)]
    public byte[][] Ss { get; set; }
    [Attributes.Parameter("bytes32", "_rawVs", 5)]
    public byte[] RawVs { get; set; }
}