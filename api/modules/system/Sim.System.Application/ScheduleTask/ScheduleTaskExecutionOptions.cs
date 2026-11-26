using Microsoft.Extensions.Options;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Application.ScheduleTask;

public sealed class ScheduleTaskExecutionOptions
{
    public const string SectionName = "ScheduleTaskExecution";

    public List<string> AllowedCommands { get; set; } = [];
}

public sealed class ScheduleTaskCommandPolicy(IOptions<ScheduleTaskExecutionOptions> options)
{
    public string ValidateCommand(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 256 ||
            value.Contains('/') ||
            value.Contains('\\') ||
            Path.IsPathFullyQualified(value))
        {
            throw new BusinessException(SystemErrorCodes.InvalidScheduleTaskConfiguration);
        }

        var command = value.Trim();
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!options.Value.AllowedCommands.Any(item =>
                !string.IsNullOrWhiteSpace(item) &&
                IsCommandName(item.Trim()) &&
                string.Equals(item.Trim(), command, comparison)))
        {
            throw new BusinessException(SystemErrorCodes.CommandNotAllowed);
        }

        return command;
    }

    public string? ValidateWorkingDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Path.IsPathFullyQualified(value))
        {
            throw new BusinessException(SystemErrorCodes.InvalidScheduleTaskConfiguration);
        }

        if (!Path.IsPathFullyQualified(value))
        {
            throw new BusinessException(SystemErrorCodes.InvalidScheduleTaskConfiguration);
        }

        return Path.GetFullPath(value);
    }

    private static bool IsCommandName(string value) =>
        value.Length <= 256 &&
        !value.Contains('/') &&
        !value.Contains('\\') &&
        !Path.IsPathFullyQualified(value);
}
