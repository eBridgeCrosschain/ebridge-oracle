using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Volo.Abp.BackgroundWorkers.Quartz;
using Volo.Abp.Threading;

namespace AElf.EventHandler.Workers;

public class ResendExceedDailyLimitWorker : QuartzBackgroundWorkerBase
{
    private readonly BridgeOptions _bridgeOptions;
    private readonly ITransmitTransactionProvider _transmitTransactionProvider; 

    public ResendExceedDailyLimitWorker(AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<BridgeOptions> bridgeOptions,
        ITransmitTransactionProvider transmitTransactionProvider
    )
    {
        _bridgeOptions = bridgeOptions.Value;
        _transmitTransactionProvider = transmitTransactionProvider;
        JobDetail = JobBuilder.Create<ResendExceedDailyLimitWorker>().WithIdentity(nameof(ResendExceedDailyLimitWorker)).Build();
        Trigger = TriggerBuilder.Create().WithIdentity(nameof(ResendExceedDailyLimitWorker)).WithCronSchedule("0 0 0 * * ?").Build();
    }

    public override async Task Execute(IJobExecutionContext workerContext)
    {
        if (!_bridgeOptions.IsTransmitter) return;
        await _transmitTransactionProvider.ReSendExceedDailyJobAsync();
    }
}