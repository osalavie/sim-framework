using Sim.Framework.Application.Contracts;
using Sim.Framework.Application.Mapping;
using Sim.Framework.Application.ModelBinding;
using Sim.Framework.Application.OutputFormatters;
using Sim.Framework.Application.Services;
using Sim.Framework.Application.Caching;
using Sim.Framework.Application.Messaging;
using Sim.Framework.Domain.Shared.Caching;
using Sim.Framework.Domain.Shared.Messaging;
using Sim.Framework.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Volo.Abp.Modularity;

namespace Sim.Framework.Application;

[DependsOn(
    typeof(FrameworkDomainModule)
)]
public class FrameworkApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Mapster 自动映射：按同名属性约定映射，无需每个类型显式配置
        context.Services.AddSingleton<IObjectMapper, MapsterObjectMapper>();

        // 注册 IRemoteStreamContent 参数的模型绑定器（处理 multipart/form-data 文件上传）。
        // 必须早于框架对 FromBody 的默认推断；放在 Application 模块是因为依赖
        // RemoteStreamContent 具体类，而 Core 层不应反向依赖 Application。
        context.Services.Configure<MvcOptions>(options =>
        {
            options.ModelBinderProviders.Insert(0, new RemoteStreamContentModelBinderProvider());
            options.OutputFormatters.Insert(0, new RemoteStreamContentOutputFormatter());
        });

        var configuration = context.Services.GetConfiguration();
        context.Services.Configure<MessagingOptions>(
            configuration.GetSection(MessagingOptions.SectionName));
        context.Services.TryAddSingleton<MessageHandlerRegistry>();
        context.Services.TryAddSingleton<MessageDispatcher>();
        var messageProvider = configuration[$"{MessagingOptions.SectionName}:Provider"] ?? "Local";

        context.Services.TryAddSingleton<IMessageTransport>(provider =>
            messageProvider.ToLowerInvariant() switch
            {
                "rabbitmq" => new RabbitMqMessageTransport(
                    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MessagingOptions>>(),
                    provider.GetRequiredService<
                        Microsoft.Extensions.Logging.ILogger<RabbitMqMessageTransport>>()),
                "kafka" => new KafkaMessageTransport(
                    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MessagingOptions>>(),
                    provider.GetRequiredService<
                        Microsoft.Extensions.Logging.ILogger<KafkaMessageTransport>>()),
                "rocketmq" => new RocketMqMessageTransport(
                    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MessagingOptions>>(),
                    provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>(),
                    provider.GetRequiredService<
                        Microsoft.Extensions.Logging.ILogger<RocketMqMessageTransport>>()),
                "local" => new LocalMessageTransport(),
                _ => throw new InvalidOperationException(
                    $"Unsupported Framework:Messaging:Provider '{messageProvider}'.")
            });
        context.Services.TryAddScoped<IMessageBus, OutboxMessageBus>();
        var cachingSection = configuration.GetSection(CachingOptions.SectionName);
        var caching = new CachingOptions
        {
            Provider = cachingSection["Provider"] ?? "Local",
            RedisConnectionString = cachingSection["RedisConnectionString"],
            InstanceName = cachingSection["InstanceName"] ?? "sim:"
        };
        context.Services.Configure<CachingOptions>(
            configuration.GetSection(CachingOptions.SectionName));
        context.Services.TryAddSingleton<LocalCache>();
        if (caching.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase) ||
            caching.Provider.Equals("Hybrid", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(caching.RedisConnectionString))
            {
                throw new InvalidOperationException(
                    "Framework:Caching:RedisConnectionString is required for Redis or Hybrid caching.");
            }

            context.Services.TryAddSingleton<IConnectionMultiplexer>(_ =>
            {
                var redisOptions = ConfigurationOptions.Parse(caching.RedisConnectionString);
                redisOptions.AbortOnConnectFail = false;
                redisOptions.ClientName = "sim-framework";
                redisOptions.ConnectRetry = Math.Max(redisOptions.ConnectRetry, 3);
                redisOptions.ReconnectRetryPolicy = new ExponentialRetry(5000);
                return ConnectionMultiplexer.Connect(redisOptions);
            });
            context.Services.TryAddSingleton<RedisCache>();
            context.Services.TryAddSingleton<HybridCache>();
        }

        context.Services.TryAddSingleton<ICache>(provider =>
            caching.Provider.ToLowerInvariant() switch
            {
                "redis" => (ICache)provider.GetRequiredService<RedisCache>(),
                "hybrid" => provider.GetRequiredService<HybridCache>(),
                "local" => provider.GetRequiredService<LocalCache>(),
                _ => throw new InvalidOperationException(
                    $"Unsupported Framework:Caching:Provider '{caching.Provider}'.")
            });
        context.Services.AddHostedService<MessageOutboxHostedService>();
    }
}
