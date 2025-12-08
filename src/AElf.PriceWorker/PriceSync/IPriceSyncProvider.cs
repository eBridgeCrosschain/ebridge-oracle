using System.Threading.Tasks;

namespace AElf.PriceWorker.PriceSync;

public interface IPriceSyncProvider
{
    Task ExecuteAsync();
}