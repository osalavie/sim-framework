using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Sim.System.Application.Notifications;
using Sim.System.Application.FileStorage;
using Sim.System.Application.ScheduleTask;

namespace Sim.System.Application;

public sealed class SystemManagementStartupService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<FileStorageConfigurationInitializer>()
            .EnsureInitializedAsync();
        await scope.ServiceProvider
            .GetRequiredService<NotificationConfigurationInitializer>()
            .EnsureInitializedAsync(cancellationToken);
        await scope.ServiceProvider
            .GetRequiredService<QuartzDatabaseInitializer>()
            .EnsureTablesAsync(cancellationToken);
        await scope.ServiceProvider
            .GetRequiredService<ScheduleTaskRuntime>()
            .EnsureInitializedAsync(cancellationToken);

        // Jobs are registered above; start the scheduler so triggers begin firing.
        // (Replaces the lifecycle previously provided by ABP's Quartz module.)
        var scheduler = await scope.ServiceProvider
            .GetRequiredService<ISchedulerFactory>()
            .GetScheduler(cancellationToken);
        await scheduler.Start(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var scheduler = await scope.ServiceProvider
            .GetRequiredService<ISchedulerFactory>()
            .GetScheduler(cancellationToken);
        await scheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
    }
}
