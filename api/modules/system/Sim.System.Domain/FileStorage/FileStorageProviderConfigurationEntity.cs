using Sim.SqlSugar.Core.Attributes;
using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.System.Domain.FileStorage;

[CodeFirst]
[SugarTable("system_file_storage_provider")]
[SugarIndex("uidx_system_file_storage_provider_name", nameof(Provider), OrderByType.Asc, true)]
public class FileStorageProviderConfigurationEntity : AuditedAggregateRoot
{
    public FileStorageProviderConfigurationEntity()
    {
    }

    public FileStorageProviderConfigurationEntity(Guid id) : base(id)
    {
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Provider { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public bool Enabled { get; set; }

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? RootPath { get; set; }

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? Endpoint { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Region { get; set; }

    [SugarColumn(Length = 256, IsNullable = true)]
    public string? Bucket { get; set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string? AccessKeyId { get; set; }

    [SugarColumn(Length = 4096, IsNullable = true)]
    public string? ProtectedAccessKeySecret { get; set; }

    [SugarColumn(Length = 4096, IsNullable = true)]
    public string? ProtectedSessionToken { get; set; }

    [SugarColumn(IsNullable = false)]
    public bool ForcePathStyle { get; set; }
}
