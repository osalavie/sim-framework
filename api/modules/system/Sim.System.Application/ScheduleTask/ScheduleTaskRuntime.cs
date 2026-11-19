using Quartz;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.ScheduleTask;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.ScheduleTask;

namespace Sim.System.Application.ScheduleTask;

public sealed class ScheduleTaskRuntime(
    IEnumerable<IScheduleTaskDefinitionProvider> providers,
    ISqlSugarRepository<ScheduleTaskConfigurationEntity> repository,
    ISchedulerFactory schedulerFactory)
{
    public IReadOnlyCollection<ScheduleTaskDefinition> Definitions => providers
        .SelectMany(provider => provider.GetDefinitions())
        .GroupBy(item => (item.Group, item.Key))
        .Select(group => group.Single())
        .ToArray();

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        var configurations = await repository.GetAll();
        foreach (var definition in Definitions)
        {
            var configuration = configurations.FirstOrDefault(item =>
                string.Equals(item.Group, definition.Group, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Key, definition.Key, StringComparison.OrdinalIgnoreCase));
            if (configuration == null)
            {
                configuration = new ScheduleTaskConfigurationEntity(Guid.NewGuid())
                {
                    Key = definition.Key,
                    Group = definition.Group,
                    DisplayName = definition.DisplayName,
                    Description = definition.Description,
                    CronExpression = definition.DefaultCronExpression,
                    Enabled = definition.DefaultEnabled,
                    TaskType = ScheduleTaskTypes.Worker,
                    IsBuiltIn = true
                };
                await repository.InsertAsync(configuration);
            }
            else if (!configuration.IsBuiltIn ||
                     configuration.TaskType != ScheduleTaskTypes.Worker ||
                     configuration.DisplayName != definition.DisplayName ||
                     configuration.Description != definition.Description)
            {
                configuration.IsBuiltIn = true;
                configuration.TaskType = ScheduleTaskTypes.Worker;
                configuration.DisplayName = definition.DisplayName;
                configuration.Description = definition.Description;
                configuration.ProtectedConfiguration = null;
                await repository.UpdateAsync(configuration);
            }
        }

        configurations = await repository.GetAll();
        foreach (var configuration in configurations)
        {
            await ApplyAsync(configuration, cancellationToken);
        }
    }

    public async Task ApplyAsync(
        ScheduleTaskConfigurationEntity configuration,
        CancellationToken cancellationToken = default)
    {
        if (!CronExpression.IsValidExpression(configuration.CronExpression))
        {
            throw new BusinessException(SystemErrorCodes.InvalidCronExpression);
        }

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(configuration.Key, configuration.Group);
        var definition = configuration.IsBuiltIn
            ? Definitions.SingleOrDefault(item =>
                string.Equals(item.Group, configuration.Group, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Key, configuration.Key, StringComparison.OrdinalIgnoreCase))
            : null;
        if (configuration.IsBuiltIn && definition == null)
        {
            return;
        }

        if (!await scheduler.CheckExists(jobKey, cancellationToken))
        {
            var jobBuilder = JobBuilder.Create(
                    definition?.JobType ?? typeof(DynamicScheduleTaskJob))
                .WithIdentity(jobKey)
                .WithDescription(configuration.Description)
                .StoreDurably();
            if (!configuration.IsBuiltIn)
            {
                jobBuilder.UsingJobData(
                    DynamicScheduleTaskJob.ConfigurationIdKey,
                    configuration.Id.ToString("D"));
            }

            var job = jobBuilder.Build();
            await scheduler.AddJob(job, false, cancellationToken);
        }

        var triggerKey = TriggerKeyFor(jobKey);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithDescription(configuration.Description)
            .WithCronSchedule(configuration.CronExpression, builder =>
                builder.WithMisfireHandlingInstructionDoNothing())
            .Build();
        var existingTrigger = await scheduler.GetTrigger(triggerKey, cancellationToken);
        if (existingTrigger == null)
        {
            await scheduler.ScheduleJob(trigger, cancellationToken);
        }
        else if (RequiresReschedule(
                     existingTrigger,
                     jobKey,
                     configuration.CronExpression,
                     configuration.Description))
        {
            await scheduler.RescheduleJob(triggerKey, trigger, cancellationToken);
        }

        if (configuration.Enabled)
        {
            await scheduler.ResumeTrigger(triggerKey, cancellationToken);
        }
        else
        {
            await scheduler.PauseTrigger(triggerKey, cancellationToken);
        }
    }

    public static TriggerKey TriggerKeyFor(JobKey jobKey) =>
        new($"{jobKey.Name}-trigger", jobKey.Group);

    internal static bool RequiresReschedule(
        ITrigger existingTrigger,
        JobKey jobKey,
        string cronExpression,
        string? description) =>
        existingTrigger is not ICronTrigger cronTrigger ||
        !existingTrigger.JobKey.Equals(jobKey) ||
        existingTrigger.MisfireInstruction != MisfireInstruction.CronTrigger.DoNothing ||
        !string.Equals(
            cronTrigger.CronExpressionString,
            cronExpression,
            StringComparison.Ordinal) ||
        !string.Equals(existingTrigger.Description, description, StringComparison.Ordinal);

    public async Task RemoveAsync(
        ScheduleTaskConfigurationEntity configuration,
        CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(configuration.Key, configuration.Group);
        var triggerKey = TriggerKeyFor(jobKey);
        if (await scheduler.CheckExists(triggerKey, cancellationToken))
        {
            await scheduler.UnscheduleJob(triggerKey, cancellationToken);
        }

        if (await scheduler.CheckExists(jobKey, cancellationToken))
        {
            await scheduler.DeleteJob(jobKey, cancellationToken);
        }
    }
}
