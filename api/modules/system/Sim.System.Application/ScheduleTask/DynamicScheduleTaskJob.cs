using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Quartz;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.ScheduleTask;
using Sim.System.Domain.ScheduleTask;

namespace Sim.System.Application.ScheduleTask;

[DisallowConcurrentExecution]
public sealed class DynamicScheduleTaskJob(
    ISqlSugarRepository<ScheduleTaskConfigurationEntity> repository,
    ScheduleTaskConfigurationProtection protection,
    ScheduleTaskCommandPolicy commandPolicy,
    IHttpClientFactory httpClientFactory,
    IEnumerable<IScheduleTaskServiceMethodHandler> serviceMethodHandlers,
    ILogger<DynamicScheduleTaskJob> logger) : IJob
{
    public const string ConfigurationIdKey = "ScheduleTaskConfigurationId";
    public const string HttpClientName = "ScheduleTaskHttp";

    public async Task Execute(IJobExecutionContext context)
    {
        var idValue = context.MergedJobDataMap.GetString(ConfigurationIdKey);
        if (!Guid.TryParse(idValue, out var id))
        {
            throw new InvalidOperationException("Scheduled task configuration id is invalid.");
        }

        var entity = await repository.GetAsync(id);
        if (entity == null || entity.IsBuiltIn || !entity.Enabled)
        {
            return;
        }

        var configuration = protection.Unprotect(entity.ProtectedConfiguration);
        logger.LogInformation(
            "Executing scheduled task {TaskId} ({TaskType})",
            entity.Id,
            entity.TaskType);
        switch (entity.TaskType)
        {
            case ScheduleTaskTypes.Http:
                await ExecuteHttpAsync(configuration, context.CancellationToken);
                break;
            case ScheduleTaskTypes.Command:
                await ExecuteCommandAsync(configuration, context.CancellationToken);
                break;
            case ScheduleTaskTypes.ServiceMethod:
                await ExecuteServiceMethodAsync(configuration, context.CancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported scheduled task type: {entity.TaskType}");
        }
    }

    private async Task ExecuteHttpAsync(
        ScheduleTaskExecutionConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var uri = await ScheduleTaskHttpTargetValidator.ValidateAsync(configuration.Url, cancellationToken);
        using var request = new HttpRequestMessage(
            new HttpMethod(configuration.HttpMethod ?? HttpMethod.Get.Method),
            uri);
        foreach (var header in configuration.HttpHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(configuration.HttpBody) &&
            request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            request.Content = new StringContent(
                configuration.HttpBody,
                Encoding.UTF8,
                configuration.HttpContentType ?? "application/json");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        logger.LogInformation(
            "Scheduled HTTP request completed with status {StatusCode}",
            (int)response.StatusCode);
        response.EnsureSuccessStatusCode();
    }

    private async Task ExecuteCommandAsync(
        ScheduleTaskExecutionConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var command = commandPolicy.ValidateCommand(configuration.Command);
        var workingDirectory = commandPolicy.ValidateWorkingDirectory(configuration.WorkingDirectory);
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in configuration.CommandArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start the scheduled process.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));
        var standardOutput = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
        var standardError = process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
        try
        {
            await Task.WhenAll(
                process.WaitForExitAsync(timeout.Token),
                standardOutput,
                standardError);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("The scheduled process exceeded its execution timeout.");
        }

        logger.LogInformation(
            "Scheduled process exited with code {ExitCode}",
            process.ExitCode);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Scheduled process exited with code {process.ExitCode}.");
        }
    }

    private async Task ExecuteServiceMethodAsync(
        ScheduleTaskExecutionConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var handler = serviceMethodHandlers.SingleOrDefault(item =>
            string.Equals(item.Key, configuration.ServiceMethodKey, StringComparison.OrdinalIgnoreCase));
        if (handler == null)
        {
            throw new InvalidOperationException("The scheduled service method is not registered.");
        }

        await handler.ExecuteAsync(configuration.ServiceArgumentsJson, cancellationToken);
    }
}
