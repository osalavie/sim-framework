using Sim.Framework.Domain.Entities;
using SqlSugar;

namespace Sim.System.Domain.FileManagement;

[SugarTable("system_resource_file_reference")]
[SugarIndex(
    "uk_system_resource_file_reference",
    nameof(ResourceType),
    OrderByType.Asc,
    nameof(ResourceId),
    OrderByType.Asc,
    nameof(FileId),
    OrderByType.Asc,
    true)]
public sealed class ResourceFileReferenceEntity : AuditedEntity
{
    [SugarColumn(Length = 64, IsNullable = false)]
    public string ResourceType { get; set; } = string.Empty;

    public Guid ResourceId { get; set; }

    public Guid FileId { get; set; }
}
