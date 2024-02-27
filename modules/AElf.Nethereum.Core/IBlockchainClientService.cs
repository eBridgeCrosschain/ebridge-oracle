using AElf.Nethereum.Core.Dtos;

namespace AElf.Nethereum.Core;

public interface IBlockchainClientService
{
    List<string> GetClientAliasList();
    Task<long> GetBlockNumberAsync(string clientAlias);
    Task<TransactionReceiptDto?> GetTransactionReceiptAsync(string clientAlias, string transactionHash);
    Task<BlockDto> GetBlockByNumberAsync(string clientAlias, long number);
}