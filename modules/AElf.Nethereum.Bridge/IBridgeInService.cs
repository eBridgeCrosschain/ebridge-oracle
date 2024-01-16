using AElf.Nethereum.Bridge.Dtos;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Bridge;

public interface IBridgeInService
{
    Task<ReceiptInfosDto> GetSendReceiptInfosAsync(string chainId, string contractAddress, string token, string targetChainId, long fromIndex,long endIndex);

    Task<SendReceiptIndexDto> GetTransferReceiptIndexAsync(string chainId, string contractAddress, List<string> tokens,
        List<string> targetChainIds);
}

public class BridgeInService : ClientProviderAggregatorBase<IClientBridgeInService>, IBridgeInService, ITransientDependency
{
    public async Task<ReceiptInfosDto> GetSendReceiptInfosAsync(string chainId, string contractAddress,
        string token, string targetChainId, long fromIndex,long endIndex)
    {
        var clientProvider = GetClientProvider(chainId);
        return await clientProvider.GetSendReceiptInfosAsync(chainId, contractAddress, token, targetChainId, fromIndex,endIndex);
    }

    public async Task<SendReceiptIndexDto> GetTransferReceiptIndexAsync(string chainId, string contractAddress,
        List<string> tokens, List<string> targetChainIds)
    {
        var clientProvider = GetClientProvider(chainId);
        return await clientProvider.GetTransferReceiptIndexAsync(chainId, contractAddress, tokens, targetChainIds);
    }

    protected BridgeInService(IEnumerable<IClientBridgeInService> clientProviders) : base(clientProviders)
    {
    }
}