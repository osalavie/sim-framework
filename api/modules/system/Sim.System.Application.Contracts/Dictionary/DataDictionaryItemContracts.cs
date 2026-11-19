using System.ComponentModel.DataAnnotations;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.Dictionary;

public sealed class DataDictionaryItemDto
{
    public Guid Id { get; set; }
    public Guid DictionaryId { get; set; }
    public string ItemKey { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sort { get; set; }
    public bool Enabled { get; set; }
}

public sealed class CreateDataDictionaryItemDto
{
    [Required]
    public Guid DictionaryId { get; set; }

    [Required]
    [MaxLength(128)]
    public string ItemKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [Range(0, int.MaxValue)]
    public int Sort { get; set; }

    public bool Enabled { get; set; } = true;
}

public sealed class UpdateDataDictionaryItemDto
{
    [Required]
    [MaxLength(256)]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [Range(0, int.MaxValue)]
    public int Sort { get; set; }

    public bool Enabled { get; set; } = true;
}

public interface IDataDictionaryItemService : IApplicationService
{
    Task<List<DataDictionaryItemDto>> GetListAsync(
        Guid? dictionaryId = null,
        CancellationToken cancellationToken = default);

    Task<DataDictionaryItemDto> CreateAsync(
        CreateDataDictionaryItemDto input,
        CancellationToken cancellationToken = default);

    Task<DataDictionaryItemDto> UpdateAsync(
        Guid id,
        UpdateDataDictionaryItemDto input,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
