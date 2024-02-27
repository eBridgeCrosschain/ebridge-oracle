using System.Numerics;
using AElf.Nethereum.Bridge.Dtos;
using AElf.Nethereum.Core;
using Nethereum.Contracts;
using TronClient;
using TronNet.ABI.FunctionEncoding.Attributes;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Bridge.Tron;

[Function("getSendReceiptInfos", "tuple[]")]
public class GetSendReceiptInfosFunctionMessage: FunctionMessage
{
    [Parameter("address", "token", 1)]
    public string Token { get; set; }
    
    [Parameter("string", "targetChainId", 2)]
    public string TargetChainId { get; set; }
    
    [Parameter("uint256", "fromIndex", 3)]
    public BigInteger FromIndex { get; set; }
    
    [Parameter("uint256", "endIndex", 4)]
    public BigInteger EndIndex { get; set; }
}

[FunctionOutput]
public class GetReceiptInfosDto: IFunctionOutputDTO
{
    [Parameter("tuple[]", "_receipts", 1)]
    public List<ReceiptDto> Receipts { get; set; }
}

[FunctionOutput]
public class ReceiptDto : IFunctionOutputDTO
{
    [Parameter("address", "asset", 1)]
    public string Asset { get; set; }
    [Parameter("address", "owner", 2)]
    public string Owner { get; set; }
    [Parameter("uint256", "amount", 3)]
    public BigInteger Amount { get; set; }
    [Parameter("uint256", "blockHeight", 4)]
    public BigInteger BlockHeight { get; set; }
    [Parameter("uint256", "blockTime", 5)]
    public BigInteger BlockTime { get; set; }
    [Parameter("string", "targetChainId", 6)]
    public string TargetChainId { get; set; }
    [Parameter("string", "targetAddress", 7)]
    public string TargetAddress { get; set; }
    [Parameter("string", "receiptId", 8)]
    public string ReceiptId { get; set; }
}

[Function("getSendReceiptIndex", "tuple[]")]
public class GetSendReceiptIndexFunctionMessage: FunctionMessage
{
    [Parameter("address[]", "tokens", 1)]
    public List<string> Tokens { get; set; }
    
    [Parameter("string[]", "targetChainIds", 2)]
    public List<string> TargetChainIds { get; set; }
}

[FunctionOutput]
public class GetSendReceiptIndexDto: IFunctionOutputDTO
{
    [Parameter("uint256[]", "indexes", 1)]
    public List<BigInteger> Indexes { get; set; }
}

public class TronBridgeInService : IClientBridgeInService, ITransientDependency
{
    public ITronClientProvider TronClientProvider { get; set; }
    public async Task<ReceiptInfosDto> GetSendReceiptInfosAsync(string chainId, string contractAddress,
        string token, string targetChainId, long fromIndex,long endIndex)
    {
        var tronClient = TronClientProvider.GetClient(chainId);
        var contract = tronClient.GetContract(contractAddress);

        var dto = await contract.CallAsync<GetSendReceiptInfosFunctionMessage, GetReceiptInfosDto>(new TronConstantContractFunctionMessage<GetSendReceiptInfosFunctionMessage>
        {
            FunctionMessage = new GetSendReceiptInfosFunctionMessage
            {
                Token = token,
                TargetChainId = targetChainId,
                FromIndex = fromIndex,
                EndIndex = endIndex
            },
            Visible = true
        });

        return dto.ToReceiptInfoDtos();
    }

    public async Task<SendReceiptIndexDto> GetTransferReceiptIndexAsync(string chainId, string contractAddress,
        List<string> tokens, List<string> targetChainIds)
    {
        var tronClient = TronClientProvider.GetClient(chainId);
        var contract = tronClient.GetContract(contractAddress);

        var dto = await contract.CallAsync<GetSendReceiptIndexFunctionMessage, GetSendReceiptIndexDto>(new TronConstantContractFunctionMessage<GetSendReceiptIndexFunctionMessage>
        {
            FunctionMessage = new GetSendReceiptIndexFunctionMessage
            {
                Tokens = tokens,
                TargetChainIds = targetChainIds
            },
            Visible = true
        });
        
        return dto.ToSendReceiptIndexDto();
    }

    public List<string> GetClientAliasList()
    {
        return TronClientProvider.GetClientAliasList();
    }
}

internal static class ReceiptInfosDtoExtension
{
    public static ReceiptInfosDto ToReceiptInfoDtos(this GetReceiptInfosDto getReceiptInfosDto)
    {
        var receiptInfosDto = new ReceiptInfosDto();
        foreach (var receiptDto in getReceiptInfosDto.Receipts)
        {
            receiptInfosDto.Receipts.Add(new Dtos.ReceiptDto
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
    public static SendReceiptIndexDto ToSendReceiptIndexDto(this GetSendReceiptIndexDto getSendReceiptIndexDto)
    {
        var sendReceiptIndexDto = new SendReceiptIndexDto();
        foreach (var index in getSendReceiptIndexDto.Indexes)
        {
            sendReceiptIndexDto.Indexes.Add(index);
        }

        return sendReceiptIndexDto;
    }
}