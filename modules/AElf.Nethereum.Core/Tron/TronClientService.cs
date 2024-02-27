using AElf.Nethereum.Core.Dtos;
using TronNet;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Core.Tron;

public class TronClientService : IBlockchainClientService, ITransientDependency
{
    private readonly ITronClientProvider _tronClientProvider;

    public TronClientService(ITronClientProvider tronClientProvider)
    {
        _tronClientProvider = tronClientProvider;
    }

    public List<string> GetClientAliasList()
    {
        return _tronClientProvider.GetClientAliasList();
    }

    public async Task<long> GetBlockNumberAsync(string clientAlias)
    {
        var web3 = _tronClientProvider.GetClient(clientAlias);
        var result = await web3.GetNowBlockAsync();
        return result.block_header.raw_data.number;
    }

    public async Task<TransactionReceiptDto?> GetTransactionReceiptAsync(string clientAlias, string transactionHash)
    {
        var web3 = _tronClientProvider.GetClient(clientAlias);
        var transactionInfo = await web3.GetTransactionInfoByIdAsync(transactionHash);
        var block = await web3.GetBlockByNumAsync(transactionInfo.blockNumber);
        
        var status = TransactionStatus.Unknown;
        foreach(var transactionExtension in block.transactions)
        {
            var transaction = transactionExtension.transaction;
            if (transaction.txID != transactionHash)
            {
                continue;
            }
            
            if(transaction.ret != null && transaction.ret.Length > 0)
            {
                status = transaction.ret[0].contractRet == "SUCCESS" ? TransactionStatus.Success : TransactionStatus.Failed;
            }

            break;
        }
        
        var transactionReceipt = new TransactionReceiptDto()
        {
            TransactionId = transactionHash,
            BlockHash = block.blockid.ToHex(),
            BlockNumber = transactionInfo.blockNumber,
            Status = status
        };
        
        return transactionReceipt;
    }
    
    public async Task<BlockDto> GetBlockByNumberAsync(string clientAlias, long number)
    {
        var web3 = _tronClientProvider.GetClient(clientAlias);
        var result = await web3.GetBlockByNumAsync(number);
        
        var block = new BlockDto
        {
            BlockHash = result.blockid.ToHex()
        };
        
        return block;
    }
}