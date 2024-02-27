using AElf.Nethereum.Core.Dtos;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Core.Nethereum;

public class NethereumClientService : IBlockchainClientService, ITransientDependency
{
    private readonly INethereumClientProvider _nethereumClientProvider;

    public NethereumClientService(INethereumClientProvider nethereumClientProvider)
    {
        _nethereumClientProvider = nethereumClientProvider;
    }

    public List<string> GetClientAliasList()
    {
        return _nethereumClientProvider.GetClientAliasList();
    }

    public async Task<long> GetBlockNumberAsync(string clientAlias)
    {
        var web3 = _nethereumClientProvider.GetClient(clientAlias);
        var latestBlockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        return latestBlockNumber.ToLong();
    }

    public async Task<TransactionReceiptDto?> GetTransactionReceiptAsync(string clientAlias, string transactionHash)
    {
        var web3 = _nethereumClientProvider.GetClient(clientAlias);
        var result = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
        return result.ToTransactionReceiptDto();
    }
    
    public async Task<BlockDto> GetBlockByNumberAsync(string clientAlias, long number)
    {
        var numberInHex = new HexBigInteger(number);
        
        var web3 = _nethereumClientProvider.GetClient(clientAlias);
        var result = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(numberInHex);
        return result.ToBlockDto();
    }
}

internal static class TransactionReceiptExtension
{
    public static TransactionReceiptDto? ToTransactionReceiptDto(this TransactionReceipt? transactionReceipt)
    {
        if(transactionReceipt == null) return null;
        
        var status = TransactionStatus.Unknown;
        if(transactionReceipt.Status != null)
        {
            status = transactionReceipt.Status.Value == 1 ? TransactionStatus.Success : TransactionStatus.Failed;
        }
        
        return new TransactionReceiptDto
        {
            TransactionId = transactionReceipt.TransactionHash,
            BlockHash = transactionReceipt.BlockHash,
            BlockNumber = (long)transactionReceipt.BlockNumber.Value,
            Status = status
        };
    }
}

internal static class BlockWithTransactionHashesExtension
{
    public static BlockDto ToBlockDto(this BlockWithTransactionHashes blockWithTransactionHashes)
    {
        return new BlockDto
        {
            BlockHash = blockWithTransactionHashes.BlockHash
        };
    }
}