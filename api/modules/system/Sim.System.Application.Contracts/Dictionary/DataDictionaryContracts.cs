using System.ComponentModel.DataAnnotations;
using Sim.Framework.Application.Contracts.IServices;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.Contracts.Dictionary;

public sealed class DataDictionaryDto
{
    public Guid Id { get; set; }
    public string DictionaryKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DataDictionaryValueType ValueType { get; set; } = DataDictionaryValueType.String;
    public string ValuePrefix { get; set; } = string.Empty;
    public string ValueSuffix { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int ItemCount { get; set; }
}

public sealed class CreateDataDictionaryDto
{
    [Required]
    [MaxLength(128)]
    public string DictionaryKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [EnumDataType(typeof(DataDictionaryValueType))]
    public DataDictionaryValueType ValueType { get; set; } = DataDictionaryValueType.String;

    [MaxLength(32)]
    public string? ValuePrefix { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? ValueSuffix { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}

public sealed class UpdateDataDictionaryDto
{
    [Required]
    [MaxLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [EnumDataType(typeof(DataDictionaryValueType))]
    public DataDictionaryValueType ValueType { get; set; } = DataDictionaryValueType.String;

    [MaxLength(32)]
    public string? ValuePrefix { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? ValueSuffix { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}

public interface IDataDictionaryService : IApplicationService
{
    Task<List<DataDictionaryDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<DataDictionaryDto> CreateAsync(
        CreateDataDictionaryDto input,
        CancellationToken cancellationToken = default);

    Task<DataDictionaryDto> UpdateAsync(
        Guid id,
        UpdateDataDictionaryDto input,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
