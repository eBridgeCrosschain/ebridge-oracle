using AElf.Nethereum.Core.Dtos;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Core;

public interface IBlockchainService
{
    Task<long> GetBlockNumberAsync(string clientAlias);
    Task<TransactionReceiptDto?> GetTransactionReceiptAsync(string clientAlias, string transactionHash);
    Task<BlockDto> GetBlockByNumberAsync(string clientAlias, long number);
}

public class BlockchainService : IBlockchainService, ITransientDependency
{
    private readonly Dictionary<string, IBlockchainClientService> _blockchainClientServiceDict;

    public BlockchainService(IEnumerable<IBlockchainClientService> blockchainClientServices)
    {
        _blockchainClientServiceDict = new Dictionary<string, IBlockchainClientService>();
        foreach (var blockchainClientService in blockchainClientServices)
        {
            var clientAliasList = blockchainClientService.GetClientAliasList();
            foreach (var clientAlias in clientAliasList)
            {
                _blockchainClientServiceDict.Add(clientAlias, blockchainClientService);
            }
        }
    }

    public async Task<long> GetBlockNumberAsync(string clientAlias)
    {
        return await ExecuteAsync(clientAlias, async service => await service.GetBlockNumberAsync(clientAlias));
    }

    public async Task<TransactionReceiptDto?> GetTransactionReceiptAsync(string clientAlias, string transactionHash)
    {
        return await ExecuteAsync(clientAlias, async service => await service.GetTransactionReceiptAsync(clientAlias, transactionHash));
    }
    
    public async Task<BlockDto> GetBlockByNumberAsync(string clientAlias, long number)
    {
        return await ExecuteAsync(clientAlias, async service => await service.GetBlockByNumberAsync(clientAlias, number));
    }
    
    private async Task<T> ExecuteAsync<T>(string clientAlias, Func<IBlockchainClientService, Task<T>> action)
    {
        if (_blockchainClientServiceDict.TryGetValue(clientAlias, out var blockchainClientService))
        {
            return await action(blockchainClientService);
        }

        throw new Exception(clientAlias);
    }
}