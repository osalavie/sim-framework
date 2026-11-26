using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Sim.Framework.Application.Mapping;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain.Shared.Security;
using Sim.System.Domain.Shared.Security;
using Sim.System.FileManagement;
using Sim.System.Application.Authorization;
using Sim.System.Application.Contracts;
using Sim.System.Application.Contracts.ModuleConfig;
using Sim.System.Application.Contracts.Permission;
using Sim.System.Application.Contracts.Role;
using Sim.System.Application.Contracts.RoleModule;
using Sim.System.Application.Contracts.RouteConfig;
using Sim.System.Application.Contracts.ScheduleTask;
using Sim.System.Application.Contracts.SystemConfig;
using Sim.System.Application.Contracts.User;
using Sim.System.Application.Jobs;
using Sim.System.Domain;
using Sim.Framework.Application.Messaging;
using Sim.System.Application.Contracts.Notifications;
using Sim.System.Application.Contracts.Organization;
using Sim.System.Application.Notifications;
using Sim.System.Application.Notifications.Providers;
using Sim.System.Notifications;
using Sim.Framework.Domain.Shared.Numbering;
using Sim.Framework.Domain.Shared;
using Sim.SqlSugar.Core.Options;
using Volo.Abp.Modularity;
using Sim.System.Application.Dictionary;
using Sim.System.Application.E2ee;
using Sim.System.Application.FileManagement;
using Sim.System.Application.FileStorage;
using Sim.System.Application.ModuleConfig;
using Sim.System.Application.Numbering;
using Sim.System.Application.Organization;
using Sim.System.Application.Permission;
using Sim.System.Application.Role;
using Sim.System.Application.RouteConfig;
using Sim.System.Application.ScheduleTask;
using Sim.System.Application.Security;
using Sim.System.Application.Security.ExternalLogin;
using Sim.System.Application.SystemConfig;
using Sim.System.Application.User;

namespace Sim.System.Application;

[DependsOn(
    typeof(SystemDomainModule),
    typeof(SystemApplicationContractsModule)
)]
public class SystemApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<Sim.SqlSugar.Core.Migrations.IMigration, Sim.System.Application.E2ee.RemoveAccountRecoveryBackupMigration>();
        // === RBAC services (migrated from RbacApplicationModule) ===
        context.Services.AddHttpContextAccessor();
        context.Services.AddExternalLogin(context.Services.GetConfiguration());
        context.Services.AddScoped<ICurrentUser, CurrentUser>();
        context.Services.AddScoped<IPasswordPolicyProvider, PasswordPolicyProvider>();
        context.Services.AddSingleton<E2eePasskeyWebAuthn>();
        context.Services.AddSingleton<IUserSecuritySecretProtector, UserSecuritySecretProtector>();
        context.Services.AddTransient<IVerificationMessageSender, VerificationMessageSender>();
        context.Services.AddScoped<INotificationAccountDirectory, SystemNotificationAccountDirectory>();

        // RBAC application services
        context.Services.AddTransient<UserService>(sp => CreateAppService<UserService>(sp));
        context.Services.AddTransient<IUserService>(sp => sp.GetRequiredService<UserService>());
        context.Services.AddTransient<IExternalLoginAccountService>(sp => sp.GetRequiredService<UserService>());
        context.Services.AddTransient<IExternalLoginService>(sp => CreateAppService<ExternalLoginService>(sp));
        context.Services.AddTransient<IPermissionService>(sp => CreateAppService<PermissionService>(sp));
        context.Services.AddTransient<IRoleService>(sp => CreateAppService<RoleService>(sp));
        context.Services.AddTransient<IUserSessionRevoker, UserSessionRevoker>();
        context.Services.AddTransient<IRoleModuleService>(sp => CreateAppService<RoleModuleService>(sp));
        context.Services.AddTransient<IModuleConfigAppService>(sp => CreateAppService<ModuleConfigAppService>(sp));
        context.Services.AddTransient<IRouteConfigAppService>(sp => CreateAppService<RouteConfigAppService>(sp));
        context.Services.AddTransient<ISystemConfigService>(sp => CreateAppService<SystemConfigService>(sp));

        // Quartz job for expired refresh token cleanup
        context.Services.AddTransient<CleanupExpiredRefreshTokensJob>();
        context.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<
            IScheduleTaskDefinitionProvider,
            CleanupExpiredRefreshTokensScheduleTaskProvider>());
        context.Services.AddTransient<SystemHeartbeatJob>();
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<
            IScheduleTaskDefinitionProvider,
            SystemHeartbeatScheduleTaskProvider>());
        // === End RBAC services ===


        var configuration = context.Services.GetConfiguration();
        var environment = context.Services.GetSingletonInstance<IHostEnvironment>();

        // File management DI registration (migrated from FrameworkApplicationModule)
        context.Services.AddFileManagement(configuration, environment.ContentRootPath);

        // Database storage provider (requires SqlSugar, registered here instead of a separate module)
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IFileStorageProvider, DatabaseFileStorageProvider>());

        // S3-compatible object storage providers (requires AWSSDK.S3)
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IFileStorageProvider, S3FileStorageProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IFileStorageProvider, TencentCosFileStorageProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IFileStorageProvider, AliyunOssFileStorageProvider>());

        // Override the default options provider with the database-backed implementation
        context.Services.Replace(ServiceDescriptor.Scoped<
            IFileManagementOptionsProvider,
            DatabaseFileManagementOptionsProvider>());

        // Read the connection string from the unified DataBase:ConnectionConfigs section
        // (falls back to top-level ConnectionStrings for backward compatibility).
        var connectionStringName = configuration
            .GetSection("Quartz:JobStore")
            .GetValue<string>("ConnectionStringName") ?? "Default";
        var connectionString = DatabaseConnectionStringResolver.Resolve(configuration, connectionStringName);

        // Unified SQL log switch: DataBase:EnableSqlLog controls both SqlSugar AOP
        // (configured in SqlSugarCoreModule) and Quartz ADO.NET SQL logging.
        var enableSqlLog = configuration.GetValue<bool>("DataBase:EnableSqlLog");

        context.Services.AddQuartz(q =>
        {
            q.AddJobListener<ScheduleTaskExecutionHistoryListener>();
            // JobStore: configurable via Quartz:JobStore section in appsettings
            // Development default: RAMJobStore (in-memory, no configuration needed)
            // Production: configure ADO.NET persistent JobStore in appsettings.Production.json
            var jobStoreSection = configuration.GetSection("Quartz:JobStore");
            if (jobStoreSection.Exists() && !string.IsNullOrWhiteSpace(jobStoreSection.GetValue<string>("Provider")))
            {
                q.UsePersistentStore(store =>
                {
                    store.UseProperties = true;
                    store.UseNewtonsoftJsonSerializer();
                    store.PerformSchemaValidation = true;
                    store.UseClustering(c =>
                    {
                        c.CheckinMisfireThreshold =
                            TimeSpan.FromSeconds(jobStoreSection.GetValue("ClusterCheckinMisfireThreshold", 30));
                        c.CheckinInterval =
                            TimeSpan.FromMilliseconds(jobStoreSection.GetValue("ClusterCheckinIntervalMs", 20000));
                    });

                    var provider = jobStoreSection.GetValue<string>("Provider")?.ToLowerInvariant();
                    var quartzConnStr = DatabaseConnectionStringResolver.Resolve(
                        configuration,
                        jobStoreSection.GetValue<string>("ConnectionStringName") ?? "Default");

                    if (string.IsNullOrWhiteSpace(quartzConnStr))
                    {
                        throw new InvalidOperationException(
                            "Quartz JobStore connection string is not configured. " +
                            "Set the connection string in DataBase:ConnectionConfigs or specify ConnectionStringName under Quartz:JobStore.");
                    }

                    switch (provider)
                    {
                        case "sqlserver":
                            store.UseSqlServer(quartzConnStr);
                            break;
                        case "postgres":
                            store.UsePostgres(quartzConnStr);
                            break;
                        case "mysql":
                            store.UseMySql(quartzConnStr);
                            break;
                        default:
                            throw new InvalidOperationException(
                                $"Quartz JobStore provider '{provider}' is not supported. " +
                                "Supported: sqlserver, postgres, mysql.");
                    }

                    // Resolve the table prefix: explicit config value, or provider-aware default.
                    // Configured via Quartz:JobStore:TablePrefix in appsettings.json.
                    // Defaults: sim_qrtz_ (postgres/mysql) or sim_QRTZ_ (sqlserver/sqlite).
                    var tablePrefix = QuartzTablePrefixResolver.Resolve(jobStoreSection, provider);
                    store.SetProperty("quartz.jobStore.tablePrefix", tablePrefix);
                });
            }
            // else: default RAMJobStore (in-memory) for development
        });

        // Configure Quartz SQL logging based on the unified EnableSqlLog switch.
        // Quartz 3.x writes these logs through its own LibLog bridge, which auto-detects
        // Serilog and bypasses Microsoft.Extensions.Logging; the effective gate is the
        // Serilog MinimumLevel.Override applied by LoggingConfiguration.ConfigureQuartzSqlLogging
        // in the host Program.cs. The rules below cover the same SQL categories (statements are
        // logged at Debug level under "Quartz.SQL", job store internals under
        // "Quartz.Impl.AdoJobStore") for the case where Quartz is routed through Microsoft logging.
        context.Services.AddLogging(builder =>
        {
            var quartzSqlLevel = enableSqlLog ? LogLevel.Debug : LogLevel.Warning;
            builder.AddFilter("Quartz.SQL", quartzSqlLevel);
            builder.AddFilter("Quartz.Impl.AdoJobStore", quartzSqlLevel);
        });
        context.Services.Configure<ScheduleTaskExecutionOptions>(
            configuration.GetSection(ScheduleTaskExecutionOptions.SectionName));
        context.Services.AddHttpClient(DynamicScheduleTaskJob.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
        context.Services.TryAddScoped<IChunkedUploadService, ChunkedUploadService>();
        context.Services.TryAddScoped<IResourceAccessService, ResourceAccessService>();
        context.Services.AddScoped<FileStorageConfigurationProtection>();
        context.Services.AddScoped<FileStorageConfigurationInitializer>();
        context.Services.AddScoped<ScheduleTaskConfigurationProtection>();
        context.Services.AddScoped<ScheduleTaskCommandPolicy>();
        context.Services.AddScoped<ScheduleTaskRuntime>();
        context.Services.AddScoped<QuartzDatabaseInitializer>();
        context.Services.AddTransient<ScheduleTaskExecutionHistoryListener>();
        context.Services.AddTransient<DynamicScheduleTaskJob>();
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<
            Contracts.ScheduleTask.IScheduleTaskServiceMethodHandler,
            FileStorageInitializationScheduleTaskHandler>());
        if (!configuration.GetValue<bool>("App:OpenApiGeneration"))
        {
            context.Services.AddHostedService<SystemManagementStartupService>();
        }

        context.Services.AddHttpClient("NotificationProvider")
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
        context.Services.AddScoped<NotificationConfigurationProtection>();
        context.Services.TryAddScoped<BusinessNumberDefinitionService>();
        context.Services.TryAddScoped<Contracts.Numbering.IBusinessNumberDefinitionService>(provider =>
            provider.GetRequiredService<BusinessNumberDefinitionService>());
        context.Services.TryAddScoped<IBusinessNumberDefinitionStore>(provider =>
            provider.GetRequiredService<BusinessNumberDefinitionService>());
        context.Services.TryAddScoped<Contracts.Numbering.IBusinessNumberDefinitionSeeder>(provider =>
            provider.GetRequiredService<BusinessNumberDefinitionService>());
        context.Services.TryAddScoped<ConfigurationItemService>();
        context.Services.TryAddScoped<Contracts.SystemConfiguration.ISystemConfigurationItemService>(provider =>
            provider.GetRequiredService<ConfigurationItemService>());
        context.Services.TryAddScoped<DataDictionaryService>();
        context.Services.TryAddScoped<Contracts.Dictionary.IDataDictionaryService>(provider =>
            provider.GetRequiredService<DataDictionaryService>());
        context.Services.TryAddScoped<DataDictionaryItemService>();
        context.Services.TryAddScoped<Contracts.Dictionary.IDataDictionaryItemService>(provider =>
            provider.GetRequiredService<DataDictionaryItemService>());
        context.Services.AddScoped<DataDictionaryInitializer>();
        context.Services.AddScoped<IDataDictionaryReader, DataDictionaryReader>();
        context.Services.AddScoped<NotificationConfigurationInitializer>();
        context.Services.AddScoped<NotificationAccountDestinationResolver>();
        context.Services.AddScoped<UserNotificationRealtimeNotifier>();
        context.Services.AddScoped<NotificationProviderRegistry>();
        context.Services.TryAddScoped<INotificationEventPublisher, NotificationEventPublisher>();
        context.Services.TryAddScoped<ISecurityNotificationSender, SecurityNotificationSender>();
        context.Services.TryAddScoped<IUserNotificationService, UserNotificationService>();
        context.Services.TryAddScoped<INotificationAdministrationService, NotificationAdministrationService>();
        context.Services.AddMessageHandler<
            Contracts.Notifications.NotificationEvent,
            NotificationEventHandler>(NotificationMessageTypes.Event);
        context.Services.AddMessageHandler<
            Contracts.FileManagement.FileConversionMessage,
            FileConversionMessageHandler>(Contracts.FileManagement.FileConversionMessageTypes.Convert);
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, InboxNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, BrowserNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, SmtpNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, StandardSmsNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, TencentSmsNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, AliyunSmsNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, StandardVoiceNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, TencentVoiceNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, AliyunVoiceNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, GenericWebhookNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, FeishuNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, WeComNotificationProvider>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<INotificationChannelProvider, DingTalkNotificationProvider>());
        context.Services.AddHostedService<NotificationDeliveryHostedService>();

        // Organization & Data Scope
        context.Services.AddScoped<IOrganizationAppService, OrganizationAppService>();
        context.Services.AddScoped<IDataScopeResolver, DataScopeResolver>();
        context.Services.AddScoped<IDataScopeFilter, DataScopeFilter>();
        context.Services.AddScoped<IOrganizationDirectory, OrganizationDirectory>();

        // Resource Access Audit
        context.Services.AddScoped<IResourceAccessAuditService, ResourceAccessAuditService>();

        // Storage Quota
        context.Services.AddScoped<Sim.System.Application.Contracts.FileManagement.IStorageQuotaService, StorageQuotaService>();
    }

    /// <summary>
    /// Creates an application service instance and populates ApplicationService's Logger/ObjectMapper/CurrentUser properties.
    /// </summary>
    private static TService CreateAppService<TService>(IServiceProvider serviceProvider)
        where TService : ApplicationService
    {
        var service = ActivatorUtilities.CreateInstance<TService>(serviceProvider);
        service.Logger = serviceProvider.GetRequiredService<ILogger<TService>>();
        service.ObjectMapper = serviceProvider.GetRequiredService<IObjectMapper>();
        service.CurrentUser = serviceProvider.GetRequiredService<ICurrentUser>();
        return service;
    }
}
