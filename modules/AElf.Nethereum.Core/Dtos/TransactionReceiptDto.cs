namespace AElf.Nethereum.Core.Dtos;

public enum TransactionStatus
{
    Unknown = -1,
    Failed = 0,
    Success = 1,
    Mining = 2
}

public class TransactionReceiptDto
{
    public string TransactionId { get; set; }
    public string BlockHash { get; set; }
    public long BlockNumber { get; set; }
    public TransactionStatus Status { get; set; }
}