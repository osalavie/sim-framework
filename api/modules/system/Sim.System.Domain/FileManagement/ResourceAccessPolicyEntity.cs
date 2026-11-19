using Sim.Framework.Domain.Entities;
using Sim.System.Domain.Shared;
using SqlSugar;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Domain.FileManagement;

[SugarTable("system_resource_access_policy")]
[SugarIndex(
    "uk_system_resource_access_policy_resource",
    nameof(ResourceType),
    OrderByType.Asc,
    nameof(ResourceId),
    OrderByType.Asc,
    true)]
public sealed class ResourceAccessPolicyEntity : AuditedEntity
{
    [SugarColumn(Length = 64, IsNullable = false)]
    public string ResourceType { get; set; } = string.Empty;

    public Guid ResourceId { get; set; }

    public Guid OwnerId { get; set; }

    public ResourceVisibility Visibility { get; set; } = ResourceVisibility.Private;
}
