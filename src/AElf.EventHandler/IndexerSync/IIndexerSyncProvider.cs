using System.Threading.Tasks;

namespace AElf.EventHandler.IndexerSync;

public interface IIndexerSyncProvider
{
    Task ExecuteAsync(string chainId);
}