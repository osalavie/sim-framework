using System.ComponentModel.DataAnnotations;

namespace Sim.Framework.Application.Contracts.Dtos;

/// <summary>
/// Base DTO carrying an entity primary key. Replaces Volo.Abp.Application.Dtos.EntityDto&lt;TKey&gt;.
/// </summary>
public class EntityDto<TKey> where TKey : notnull
{
    /// <summary>实体主键。</summary>
    public TKey Id { get; set; } = default!;
}

/// <summary>
/// DTO for audited entities with creation/modification audit fields. 
/// Replaces Volo.Abp.Application.Dtos.AuditedEntityDto&lt;TKey&gt;.
/// </summary>
public class AuditedEntityDto<TKey> : CreationAuditedEntityDto<TKey> where TKey : notnull
{
    /// <summary>最后修改时间。</summary>
    public DateTime? LastModificationTime { get; set; }
    /// <summary>最后修改人标识。</summary>
    public Guid? LastModifierId { get; set; }
}

/// <summary>
/// DTO for creation-audited entities. Replaces Volo.Abp.Application.Dtos.CreationAuditedEntityDto&lt;TKey&gt;.
/// </summary>
public class CreationAuditedEntityDto<TKey> : EntityDto<TKey> where TKey : notnull
{
    /// <summary>创建时间。</summary>
    public DateTime CreationTime { get; set; }
    /// <summary>创建人标识。</summary>
    public Guid? CreatorId { get; set; }
}

/// <summary>
/// Paged result wrapper. Replaces Volo.Abp.Application.Dtos.PagedResultDto&lt;T&gt;.
/// </summary>
public class PagedResultDto<T>
{
    /// <summary>总记录数。</summary>
    public long TotalCount { get; set; }
    /// <summary>当前页数据列表。</summary>
    public List<T> Items { get; set; } = [];

    public PagedResultDto() { }

    public PagedResultDto(long totalCount, List<T> items)
    {
        TotalCount = totalCount;
        Items = items;
    }
}

/// <summary>
/// Paged and sorted request DTO. Replaces Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto.
/// </summary>
public class PagedAndSortedResultRequestDto : LimitedResultRequestDto
{
    /// <summary>Sorting expression, e.g. "Name asc, CreationTime desc".</summary>
    public string? Sorting { get; set; }
}

/// <summary>
/// Limited result request DTO. Replaces Volo.Abp.Application.Dtos.LimitedResultRequestDto.
/// </summary>
public class LimitedResultRequestDto
{
    /// <summary>Max number of items to return. Default 10.</summary>
    [Range(1, 500, ErrorMessage = "MaxResultCount must be between 1 and 500.")]
    public int MaxResultCount { get; set; } = 10;

    /// <summary>Number of items to skip. Default 0.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "SkipCount cannot be negative.")]
    public int SkipCount { get; set; } = 0;
}
