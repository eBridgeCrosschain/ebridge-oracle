using System;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.EventBus;

namespace AElf.EventHandler.Dto;

[BackgroundJobName("TransmitCheckArgs")]

public class TransmitCheckArgs : TransmitBase
{
    public int QueryTimes { get; set; }
}