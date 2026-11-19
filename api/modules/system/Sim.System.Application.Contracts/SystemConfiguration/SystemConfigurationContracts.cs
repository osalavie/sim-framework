using System.ComponentModel.DataAnnotations;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.SystemConfiguration;

public sealed class SystemConfigurationItemDto
{
    public Guid Id { get; set; }
    public string GroupKey { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sort { get; set; }
    public bool Enabled { get; set; }
}

public sealed class CreateSystemConfigurationItemDto
{
    [Required]
    [MaxLength(64)]
    public string GroupKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string GroupName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ConfigKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string ConfigName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [Range(0, int.MaxValue)]
    public int Sort { get; set; }

    public bool Enabled { get; set; } = true;
}

public sealed class UpdateSystemConfigurationItemDto
{
    [Required]
    [MaxLength(256)]
    public string GroupName { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string ConfigName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [Range(0, int.MaxValue)]
    public int Sort { get; set; }

    public bool Enabled { get; set; } = true;
}

public interface ISystemConfigurationItemService : IApplicationService
{
    Task<List<SystemConfigurationItemDto>> GetListAsync(
        string? groupKey = null,
        CancellationToken cancellationToken = default);

    Task<SystemConfigurationItemDto> CreateAsync(
        CreateSystemConfigurationItemDto input,
        CancellationToken cancellationToken = default);

    Task<SystemConfigurationItemDto> UpdateAsync(
        Guid id,
        UpdateSystemConfigurationItemDto input,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
