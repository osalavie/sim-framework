using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.ScheduleTask;

public sealed class ScheduleTaskConfigurationProtection(IDataProtectionProvider provider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector = provider.CreateProtector(
        DataProtectionIdentity.ScheduleTaskConfigurationPurpose);

    public string Protect(ScheduleTaskExecutionConfiguration configuration) =>
        _protector.Protect(JsonSerializer.Serialize(configuration, JsonOptions));

    public ScheduleTaskExecutionConfiguration Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ScheduleTaskExecutionConfiguration();
        }

        return JsonSerializer.Deserialize<ScheduleTaskExecutionConfiguration>(
                   _protector.Unprotect(value),
                   JsonOptions)
               ?? throw new InvalidOperationException("Scheduled task configuration is empty.");
    }
}
