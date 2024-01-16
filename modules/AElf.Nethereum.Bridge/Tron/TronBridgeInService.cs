using AElf.Nethereum.Core;
using Nethereum.RPC.Eth.DTOs;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Bridge.Tron;

public interface IBridgeInService
{
    Task<GetReceiptInfosDTO> GetSendReceiptInfosAsync(string chainId, string contractAddress, string token, string targetChainId, long fromIndex,long endIndex);

    Task<GetSendReceiptIndexDTO> GetTransferReceiptIndexAsync(string chainId, string contractAddress, List<string> tokens,
        List<string> targetChainIds);
}

public class TronBridgeInService : IBridgeInService, ITransientDependency
{
    public async Task<GetReceiptInfosDTO> GetSendReceiptInfosAsync(string chainId, string contractAddress,
        string token, string targetChainId, long fromIndex,long endIndex)
    {
        var function = GetFunction(chainId, contractAddress, "getSendReceiptInfos");

        var evmGetReceiptInfos =
            await function.CallDeserializingToObjectAsync<GetReceiptInfosDTO>(token, targetChainId, fromIndex,endIndex);
        return evmGetReceiptInfos;
    }

    public async Task<GetSendReceiptIndexDTO> GetTransferReceiptIndexAsync(string chainId, string contractAddress,
        List<string> tokens, List<string> targetChainIds)
    {
        var function = GetFunction(chainId, contractAddress, "getSendReceiptIndex");

        var evmGetReceiptInfos =
            await function.CallDeserializingToObjectAsync<GetSendReceiptIndexDTO>(tokens, targetChainIds);
        return evmGetReceiptInfos;
    }
}