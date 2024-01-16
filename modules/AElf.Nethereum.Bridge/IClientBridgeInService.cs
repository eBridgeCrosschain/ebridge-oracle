using AElf.Nethereum.Bridge.Dtos;
using AElf.Nethereum.Core;

namespace AElf.Nethereum.Bridge;

public interface IClientBridgeInService : IClientProvider
{
    Task<ReceiptInfosDto> GetSendReceiptInfosAsync(string chainId, string contractAddress, string token, string targetChainId, long fromIndex,long endIndex);

    Task<SendReceiptIndexDto> GetTransferReceiptIndexAsync(string chainId, string contractAddress, List<string> tokens,
        List<string> targetChainIds);
}