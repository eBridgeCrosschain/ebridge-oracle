// using System.Threading.Tasks;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
// using Quartz;
// using Volo.Abp.BackgroundWorkers.Quartz;
// using Volo.Abp.Threading;
//
// namespace AElf.EventHandler.Workers;
//
// public class ResendExceedDailyLimitWorker : QuartzBackgroundWorkerBase
// {
//     private readonly BridgeOptions _bridgeOptions;
//     private readonly ITransmitTransactionProvider _transmitTransactionProvider;
//     private ILogger<ResendExceedDailyLimitWorker> _logger;
//
//
//     public ResendExceedDailyLimitWorker(AbpAsyncTimer timer,
//         IServiceScopeFactory serviceScopeFactory,
//         IOptionsSnapshot<BridgeOptions> bridgeOptions,
//         IOptionsSnapshot<RetryTransmitInfoOptions> retryTransmitInfoOptions,
//         ITransmitTransactionProvider transmitTransactionProvider, ILogger<ResendExceedDailyLimitWorker> logger)
//     {
//         _bridgeOptions = bridgeOptions.Value;
//         _transmitTransactionProvider = transmitTransactionProvider;
//         _logger = logger;
//         var resendOption = retryTransmitInfoOptions.Value;
//         _logger.LogInformation("cron:{c}",resendOption.ResendExceedDailyLimitCron);
//         JobDetail = JobBuilder.Create<ResendExceedDailyLimitWorker>().WithIdentity(nameof(ResendExceedDailyLimitWorker)).Build();
//         Trigger = TriggerBuilder.Create().WithIdentity(nameof(ResendExceedDailyLimitWorker)).WithCronSchedule(resendOption.ResendExceedDailyLimitCron).Build();
//     }
//
//     public override async Task Execute(IJobExecutionContext workerContext)
//     {
//         if (!_bridgeOptions.IsTransmitter) return;
//         await _transmitTransactionProvider.ReSendExceedDailyJobAsync();
//     }
// }