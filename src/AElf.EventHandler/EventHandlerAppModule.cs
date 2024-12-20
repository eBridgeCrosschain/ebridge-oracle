using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Xml;
using AElf.Client.Bridge;
using AElf.Client.Core;
using AElf.Client.Core.Options;
using AElf.Client.MerkleTree;
using AElf.Client.Oracle;
using AElf.Client.Report;
using AElf.EventHandler.Dto;
using AElf.EventHandler.HttpClientHelper;
using AElf.EventHandler.Options;
using AElf.EventHandler.Workers;
using AElf.Nethereum.Bridge;
using AElf.Nethereum.Core;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundJobs.RabbitMQ;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.BackgroundWorkers.Quartz;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Volo.Abp.RabbitMQ;
using Volo.Abp.Threading;

namespace AElf.EventHandler;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(AElfClientModule),
    typeof(AElfClientOracleModule),
    typeof(AElfClientReportModule),
    typeof(AElfClientBridgeModule),
    typeof(AElfClientMerkleTreeModule),
    typeof(AElfNethereumBridgeModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpBackgroundJobsRabbitMqModule),
    typeof(AbpBackgroundWorkersQuartzModule) 
)]
public class EventHandlerAppModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        
        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "OracleClient:"; });

        // Just for logging.
        Configure<MessageQueueOptions>(options => { configuration.GetSection("MessageQueue").Bind(options); });

        Configure<AbpRabbitMqEventBusOptions>(options =>
        {
            var messageQueueConfig = configuration.GetSection("MessageQueue");
            options.ClientName = messageQueueConfig.GetSection("ClientName").Value;
            options.ExchangeName = messageQueueConfig.GetSection("ExchangeName").Value;
        });

        Configure<AbpRabbitMqOptions>(options =>
        {
            var messageQueueConfig = configuration.GetSection("MessageQueue");
            var hostName = messageQueueConfig.GetSection("HostName").Value;

            options.Connections.Default.HostName = hostName;
            options.Connections.Default.Port = int.Parse(messageQueueConfig.GetSection("Port").Value);
            options.Connections.Default.UserName = messageQueueConfig.GetSection("UserName").Value;
            options.Connections.Default.Password = messageQueueConfig.GetSection("Password").Value;
            options.Connections.Default.Ssl = new SslOption
            {
                Enabled = true,
                ServerName = hostName,
                Version = SslProtocols.Tls12,
                AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch |
                                         SslPolicyErrors.RemoteCertificateChainErrors
            };
            options.Connections.Default.VirtualHost = "/";
            options.Connections.Default.Uri = new Uri(messageQueueConfig.GetSection("Uri").Value);
        });

        Configure<AbpRabbitMqBackgroundJobOptions>(configuration.GetSection("AbpRabbitMqBackgroundJob"));
        Configure<OracleOptions>(configuration.GetSection("Oracle"));
        Configure<BridgeOptions>(configuration.GetSection("Bridge"));
        Configure<BlockConfirmationOptions>(configuration.GetSection("BlockConfirmation"));
        Configure<ChainIdMappingOptions>(configuration.GetSection("ChainIdMapping"));
        Configure<FaultHandlingOptions>(configuration.GetSection("FaultHandling"));
        Configure<RetryTransmitInfoOptions>(configuration.GetSection("RetryTransmitInfo"));
        Configure<IndexerSyncOptions>(configuration.GetSection("IndexerSync"));
        Configure<ExpiredTimeOptions>(configuration.GetSection("ExpiredTime"));
        Configure<SyncStateServiceOption>(configuration.GetSection("SyncStateService"));

        context.Services.AddHostedService<EventHandlerAppHostedService>();
        context.Services.AddSingleton<ITransmitTransactionProvider, TransmitTransactionProvider>();
        context.Services.AddSingleton<ISignatureRecoverableInfoProvider, SignatureRecoverableInfoProvider>();
        context.Services.AddSingleton<ILatestQueriedReceiptCountProvider, LatestQueriedReceiptCountProvider>();
        context.Services.AddTransient<ApiClient>();

        ConfigureGraphQl(context, configuration);
        
        context.Services.AddAutoMapperObjectMapper<EventHandlerAppModule>();

        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<EventHandlerAppModule>();
        });
        
        context.Services.AddHttpClient();
    }
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.AddBackgroundWorkerAsync<ReceiptSyncWorker>();
        context.AddBackgroundWorkerAsync<IndexerSyncWorker>();

        var faultHandlingOptions = context.ServiceProvider.GetRequiredService<IOptionsSnapshot<FaultHandlingOptions>>();
        if (faultHandlingOptions.Value.IsReSendFailedJob)
        {
            var service = context.ServiceProvider.GetRequiredService<ITransmitTransactionProvider>();
            AsyncHelper.RunSync(async()=> await service.ReSendFailedJobAsync());
        }
    }
    
    private void ConfigureGraphQl(ServiceConfigurationContext context,
        IConfiguration configuration)
    {
        context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
            new NewtonsoftJsonSerializer()));
        context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
    }
}