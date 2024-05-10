using AElf.Nethereum.Bridge.Dtos;
using AElf.Nethereum.Core;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Bridge.EVM;

public class EVMBridgeInService : ContractServiceBase, IClientBridgeInService, ITransientDependency
{
    protected override string SmartContractName { get; } = "BridgeIn";

    public async Task<ReceiptInfosDto> GetSendReceiptInfosAsync(string chainId, string contractAddress,
        string token, string targetChainId, long fromIndex,long endIndex)
    {
        var function = GetFunction(chainId, contractAddress, "getSendReceiptInfos");

        var evmGetReceiptInfos =
            await function.CallDeserializingToObjectAsync<GetReceiptInfosDTO>(token, targetChainId, fromIndex,endIndex);
        return evmGetReceiptInfos.ToReceiptInfoDtos();
    }

    public async Task<SendReceiptIndexDto> GetTransferReceiptIndexAsync(string chainId, string contractAddress,
        List<string> tokens, List<string> targetChainIds)
    {
        var function = GetFunction(chainId, contractAddress, "getSendReceiptIndex");

        var evmGetReceiptInfos =
            await function.CallDeserializingToObjectAsync<GetSendReceiptIndexDTO>(tokens, targetChainIds);
        return evmGetReceiptInfos.ToSendReceiptIndexDto();
    }

    public List<string> GetClientAliasList()
    {
        return GetChainIdList();
    }
}

internal static class GetReceiptInfosDtoExtension
{
    public static ReceiptInfosDto ToReceiptInfoDtos(this GetReceiptInfosDTO getReceiptInfosDto)
    {
        var receiptInfosDto = new ReceiptInfosDto();
        foreach (var receiptDto in getReceiptInfosDto.Receipts)
        {
            receiptInfosDto.Receipts.Add(new ReceiptDto
            {
                Asset = receiptDto.Asset,
                Amount = receiptDto.Amount,
                BlockHeight = receiptDto.BlockHeight,
                BlockTime = receiptDto.BlockTime,
                Owner = receiptDto.Owner,
                ReceiptId = receiptDto.ReceiptId,
                TargetAddress = receiptDto.TargetAddress,
                TargetChainId = receiptDto.TargetChainId
            });
        }

        return receiptInfosDto;
    }
}

internal static class GetSendReceiptIndexDtoExtension
{
    public static SendReceiptIndexDto ToSendReceiptIndexDto(this GetSendReceiptIndexDTO getSendReceiptIndexDto)
    {
        var sendReceiptIndexDto = new SendReceiptIndexDto();
        foreach (var index in getSendReceiptIndexDto.Indexes)
        {
            sendReceiptIndexDto.Indexes.Add(index);
        }

        return sendReceiptIndexDto;
    }
}