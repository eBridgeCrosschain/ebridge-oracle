using System;

namespace AElf.EventHandler.Dto;

public class TransmitBase
{
    public string ChainId { get; set; }
    public string BlockHash { get; set; }
    public long BlockHeight { get; set; }
    public string TargetChainId { get; set; }
    public string TargetContractAddress { get; set; }
    public string TransactionId { get; set; }
    public byte[] SwapHashId { get; set; }
    public byte[] Report { get; set; }
    public byte[][] Rs { get; set; }
    public byte[][] Ss{ get; set; }
    public byte[] RawVs { get; set; }
    public int SendTimes { get; set; }
    public DateTime? Time { get; set; }
}