using System.ComponentModel.DataAnnotations;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.Numbering;

public sealed class BusinessNumberDefinitionDto
{
    public Guid Id { get; set; }
    public string BusinessKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Format { get; set; } = string.Empty;
    public long InitialSequence { get; set; }
    public bool Enabled { get; set; }
}

public sealed class CreateBusinessNumberDefinitionDto
{
    [Required]
    [MaxLength(128)]
    public string BusinessKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(512)]
    public string Format { get; set; } = "{sequence:D6}";

    [Range(1, long.MaxValue)]
    public long InitialSequence { get; set; } = 1;

    public bool Enabled { get; set; } = true;
}

public sealed class UpdateBusinessNumberDefinitionDto
{
    [Required]
    [MaxLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(512)]
    public string Format { get; set; } = "{sequence:D6}";

    [Range(1, long.MaxValue)]
    public long InitialSequence { get; set; } = 1;

    public bool Enabled { get; set; }
}

public interface IBusinessNumberDefinitionService : IApplicationService
{
    Task<List<BusinessNumberDefinitionDto>> GetListAsync(
        CancellationToken cancellationToken = default);

    Task<BusinessNumberDefinitionDto> CreateAsync(
        CreateBusinessNumberDefinitionDto input,
        CancellationToken cancellationToken = default);

    Task<BusinessNumberDefinitionDto> UpdateAsync(
        Guid id,
        UpdateBusinessNumberDefinitionDto input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 供模块数据种子幂等补齐编号定义，使用显式租户范围，避免依赖当前请求上下文。
/// </summary>
public interface IBusinessNumberDefinitionSeeder
{
    Task EnsureAsync(
        string tenantScope,
        string businessKey,
        string displayName,
        string? description,
        string format,
        long initialSequence,
        CancellationToken cancellationToken = default);
}
