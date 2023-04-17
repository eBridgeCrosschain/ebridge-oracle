using System;
using System.Collections.Concurrent;
using Volo.Abp.DependencyInjection;

namespace AElf.EventHandler;

public class LatestQueriedReceiptCountProvider : ILatestQueriedReceiptCountProvider, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, LatestReceiptTime> _count = new();

    public long Get(string symbol)
    {
        if (_count.ContainsKey(symbol))
        {
            if (!((DateTime.UtcNow - _count[symbol].Timestamp).TotalMinutes > 5)) return _count[symbol].Count;
            _count[symbol] = new LatestReceiptTime
            {
                Timestamp = DateTime.UtcNow,
                Count = 0
            };
            return 0;

        }
        _count.TryAdd(symbol, new LatestReceiptTime
        {
            Timestamp = DateTime.UtcNow,
            Count = 0
        });
        return 0;
    }

    public void Set(DateTime time,string symbol, long count)
    {
        var timeCount = new LatestReceiptTime
        {
            Timestamp = time,
            Count = count
        };
        _count[symbol] = timeCount;
    }
    
}
public class LatestReceiptTime
{
    public DateTime Timestamp { get; set; }
    public long Count { get; set; }
}