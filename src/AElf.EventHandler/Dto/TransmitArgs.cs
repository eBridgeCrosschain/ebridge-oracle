using System;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.EventBus;

namespace AElf.EventHandler.Dto;

[BackgroundJobName("TransmitArgs")]
public class TransmitArgs : TransmitBase
{
    public int SendTimes { get; set; }

}