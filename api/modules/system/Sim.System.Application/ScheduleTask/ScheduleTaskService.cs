using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Quartz;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.ScheduleTask;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.ScheduleTask;

namespace Sim.System.Application.ScheduleTask;

[Authorize]
[Permission(SystemManagementPermissions.ScheduleTaskManage)]
public class ScheduleTaskService(
    ISqlSugarRepository<ScheduleTaskConfigurationEntity> repository,
    ScheduleTaskRuntime runtime,
    ScheduleTaskConfigurationProtection protection,
    ScheduleTaskCommandPolicy commandPolicy,
    IEnumerable<IScheduleTaskServiceMethodHandler> serviceMethodHandlers,
    ISchedulerFactory schedulerFactory,
    ILogger<ScheduleTaskService> logger) : ApplicationService, IScheduleTaskService
{
    private static readonly HashSet<string> AllowedHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD"
    };

    private static readonly HashSet<string> ForbiddenHttpHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Content-Length", "Transfer-Encoding", "Connection"
    };

    /// <summary>
    /// 获取定时任务列表。
    /// </summary>
    public async Task<List<ScheduleTaskDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        await runtime.EnsureInitializedAsync(cancellationToken);
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var result = new List<ScheduleTaskDto>();
        foreach (var configuration in (await repository.GetAll())
                     .OrderByDescending(item => item.IsBuiltIn)
                     .ThenBy(item => item.Group)
                     .ThenBy(item => item.DisplayName))
        {
            result.Add(await MapAsync(configuration, scheduler, cancellationToken));
        }

        return result;
    }

    /// <summary>
    /// 获取可作为定时任务执行的服务方法列表。
    /// </summary>
    public Task<List<ScheduleTaskServiceMethodDto>> GetServiceMethodsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(serviceMethodHandlers
            .OrderBy(item => item.DisplayName)
            .Select(item => new ScheduleTaskServiceMethodDto
            {
                Key = item.Key,
                DisplayName = item.DisplayName,
                Description = item.Description
            })
            .ToList());
    }

    /// <summary>
    /// 创建定时任务。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<ScheduleTaskDto> CreateAsync(
        CreateScheduleTaskDto input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.DisplayName))
        {
            throw new BusinessException(SystemErrorCodes.InvalidScheduleTaskConfiguration);
        }

        ValidateCron(input.CronExpression);
        var taskType = NormalizeTaskType(input.TaskType);
        var configuration = await BuildConfigurationAsync(input, taskType, cancellationToken);
        var id = Guid.NewGuid();
        var entity = new ScheduleTaskConfigurationEntity(id)
        {
            Key = $"dynamic-{id:N}",
            Group = "Custom",
            DisplayName = input.DisplayName.Trim(),
            Description = Normalize(input.Description),
            CronExpression = input.CronExpression.Trim(),
            Enabled = input.Enabled,
            TaskType = taskType,
            IsBuiltIn = false,
            ProtectedConfiguration = protection.Protect(configuration)
        };
        await repository.InsertAsync(entity);
        await runtime.ApplyAsync(entity, cancellationToken);
        logger.LogInformation(
            "Scheduled task {TaskId} created. Type={TaskType}; Enabled={Enabled}",
            entity.Id,
            entity.TaskType,
            entity.Enabled);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        return await MapAsync(entity, scheduler, cancellationToken);
    }

    /// <summary>
    /// 更新指定定时任务的 Cron 表达式与启用状态。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<ScheduleTaskDto> UpdateAsync(
        string group,
        string key,
        UpdateScheduleTaskDto input,
        CancellationToken cancellationToken = default)
    {
        ValidateCron(input.CronExpression);
        await runtime.EnsureInitializedAsync(cancellationToken);
        var configuration = Find(await repository.GetAll(), group, key)
                   ?? throw new BusinessException(SystemErrorCodes.ScheduleTaskNotFound);
        configuration.CronExpression = input.CronExpression.Trim();
        configuration.Enabled = input.Enabled;
        await repository.UpdateAsync(configuration);
        await runtime.ApplyAsync(configuration, cancellationToken);
        logger.LogInformation(
            "Scheduled task {TaskId} updated. Enabled={Enabled}",
            configuration.Id,
            configuration.Enabled);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        return await MapAsync(configuration, scheduler, cancellationToken);
    }

    /// <summary>
    /// 删除指定的定时任务；内置任务不可删除。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var configuration = (await repository.GetAll()).FirstOrDefault(item => item.Id == id)
                   ?? throw new BusinessException(SystemErrorCodes.ScheduleTaskNotFound);
        if (configuration.IsBuiltIn)
        {
            throw new BusinessException(SystemErrorCodes.BuiltInScheduleTaskCannotBeDeleted);
        }

        await runtime.RemoveAsync(configuration, cancellationToken);
        await repository.DeleteAsync(configuration.Id);
        logger.LogInformation(
            "Scheduled task {TaskId} deleted. Type={TaskType}",
            configuration.Id,
            configuration.TaskType);
    }

    /// <summary>
    /// 立即触发指定的定时任务。
    /// </summary>
    public async Task TriggerNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var configuration = await repository.GetAsync(id)
                   ?? throw new BusinessException(SystemErrorCodes.ScheduleTaskNotFound);
        if (!configuration.Enabled)
        {
            throw new BusinessException(SystemErrorCodes.ScheduleTaskDisabled);
        }

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(configuration.Key, configuration.Group);
        if (!await scheduler.CheckExists(jobKey, cancellationToken))
        {
            throw new BusinessException(SystemErrorCodes.ScheduleTaskNotRegistered);
        }

        await scheduler.TriggerJob(jobKey, cancellationToken);
        logger.LogInformation(
            "Scheduled task {TaskId} ({TaskKey}) triggered manually",
            configuration.Id,
            configuration.Key);
    }

    private async Task<ScheduleTaskExecutionConfiguration> BuildConfigurationAsync(
        CreateScheduleTaskDto input,
        string taskType,
        CancellationToken cancellationToken)
    {
        switch (taskType)
        {
            case ScheduleTaskTypes.Http:
                var method = input.HttpMethod?.Trim().ToUpperInvariant();
                if (method == null || !AllowedHttpMethods.Contains(method) ||
                    input.HttpHeaders?.Count > 32 ||
                    input.HttpHeaders?.Any(item =>
                        string.IsNullOrWhiteSpace(item.Key) ||
                        item.Key.Length > 128 ||
                        item.Value == null ||
                        item.Value.Length > 4096 ||
                        ForbiddenHttpHeaders.Contains(item.Key)) == true)
                {
            throw new BusinessException(SystemErrorCodes.InvalidScheduleTaskConfiguration);
                }

                var uri = await ScheduleTaskHttpTargetValidator.ValidateAsync(input.Url, cancellationToken);
                return new ScheduleTaskExecutionConfiguration
                {
                    HttpMethod = method,
                    Url = uri.AbsoluteUri,
                    HttpHeaders = input.HttpHeaders == null
                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(input.HttpHeaders, StringComparer.OrdinalIgnoreCase),
                    HttpBody = input.HttpBody,
                    HttpContentType = Normalize(input.HttpContentType) ?? "application/json"
                };

            case ScheduleTaskTypes.Command:
                if (input.CommandArguments?.Count > 64 ||
                    input.CommandArguments?.Any(argument =>
                        argument == null || argument.Length > 2048) == true ||
                    input.TimeoutSeconds is < 1 or > 3600)
                {
            throw new BusinessException(SystemErrorCodes.InvalidScheduleTaskConfiguration);
                }

                return new ScheduleTaskExecutionConfiguration
                {
                    Command = commandPolicy.ValidateCommand(input.Command ?? GetLegacyCommand(input)),
                    CommandArguments = input.CommandArguments?.ToList() ?? [],
                    WorkingDirectory = commandPolicy.ValidateWorkingDirectory(input.WorkingDirectory),
                    TimeoutSeconds = input.TimeoutSeconds
                };

            case ScheduleTaskTypes.ServiceMethod:
                var handler = serviceMethodHandlers.SingleOrDefault(item =>
                    string.Equals(item.Key, input.ServiceMethodKey, StringComparison.OrdinalIgnoreCase));
                if (handler == null)
                {
            throw new BusinessException(SystemErrorCodes.ServiceMethodNotRegistered);
                }

                if (!string.IsNullOrWhiteSpace(input.ServiceArgumentsJson))
                {
                    try
                    {
                        JsonDocument.Parse(input.ServiceArgumentsJson).Dispose();
                    }
                    catch (JsonException)
                    {
            throw new BusinessException(SystemErrorCodes.InvalidScheduleTaskConfiguration);
                    }
                }

                return new ScheduleTaskExecutionConfiguration
                {
                    ServiceMethodKey = handler.Key,
                    ServiceArgumentsJson = Normalize(input.ServiceArgumentsJson)
                };

            default:
            throw new BusinessException(SystemErrorCodes.InvalidScheduleTaskType);
        }
    }

    private static string NormalizeTaskType(string value)
    {
        var taskType = ScheduleTaskTypes.DynamicTypes.FirstOrDefault(item =>
            string.Equals(item, value.Trim(), StringComparison.OrdinalIgnoreCase));
        return taskType ?? throw new BusinessException(SystemErrorCodes.InvalidScheduleTaskType);
    }

    private static void ValidateCron(string value)
    {
        if (!CronExpression.IsValidExpression(value))
        {
            throw new BusinessException(SystemErrorCodes.InvalidCronExpression);
        }
    }

    private static ScheduleTaskConfigurationEntity? Find(
        IEnumerable<ScheduleTaskConfigurationEntity> configurations,
        string group,
        string key) => configurations.FirstOrDefault(item =>
        string.Equals(item.Group, group, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));

    private static async Task<ScheduleTaskDto> MapAsync(
        ScheduleTaskConfigurationEntity configuration,
        IScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var jobKey = new JobKey(configuration.Key, configuration.Group);
        var triggerKey = ScheduleTaskRuntime.TriggerKeyFor(jobKey);
        var trigger = await scheduler.GetTrigger(triggerKey, cancellationToken);
        var state = await scheduler.GetTriggerState(triggerKey, cancellationToken);
        return new ScheduleTaskDto
        {
            Id = configuration.Id,
            Key = configuration.Key,
            Group = configuration.Group,
            DisplayName = configuration.DisplayName,
            Description = configuration.Description,
            CronExpression = configuration.CronExpression,
            Enabled = configuration.Enabled,
            TaskType = configuration.TaskType,
            IsBuiltIn = configuration.IsBuiltIn,
            Status = state.ToString(),
            PreviousRunTime = trigger?.GetPreviousFireTimeUtc(),
            NextRunTime = trigger?.GetNextFireTimeUtc()
        };
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? GetLegacyCommand(CreateScheduleTaskDto input)
    {
#pragma warning disable CS0618
        return input.ExecutablePath;
#pragma warning restore CS0618
    }
}
