using System;
using Volo.Abp.EventBus;

namespace AElf.EventHandler.Dto;

[EventName("TransmitEto")]
public class TransmitEto : TransmitBase
{
    public DateTime? LastSendTransmitTime { get; set; }
}