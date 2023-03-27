namespace AElf.EventHandler;

public class RetryTransmitInfoOptions
{
    public int RetryTransmitTimePeriod { get; set; }
    public int MaxSendTransmitTimes { get; set; }
    public int RetryTransmitCheckTimePeriod {get; set; }
    public int MaxQueryTransmitTimes {get; set; }
}