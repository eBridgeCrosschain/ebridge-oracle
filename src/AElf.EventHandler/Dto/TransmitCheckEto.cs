using System;
using Volo.Abp.EventBus;

namespace AElf.EventHandler.Dto;

[EventName("TransmitCheckEto")]
public class TransmitCheckEto : TransmitBase
{
    public int QueryTimes { get; set; }
    public DateTime? LastSendTransmitCheckTime { get; set; }
}