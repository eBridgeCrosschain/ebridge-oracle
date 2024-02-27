using AElf.Nethereum.Core;

namespace AElf.Nethereum.Bridge;

public interface IClientBridgeOutService : IClientProvider
{
    Task<string> TransmitAsync(string chainId, string contractAddress, byte[] swapHashId, byte[] report, byte[][] rs, byte[][] ss, byte[] rawVs);
}