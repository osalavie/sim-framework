using Sim.SqlSugar.Core.Attributes;
using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.System.Domain.FileStorage;

[CodeFirst]
[SugarTable("system_file_storage_settings")]
public class FileStorageSettingsEntity : AuditedAggregateRoot
{
    public FileStorageSettingsEntity()
    {
    }

    public FileStorageSettingsEntity(Guid id) : base(id)
    {
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string DefaultProvider { get; set; } = "local";

    [SugarColumn(IsNullable = false)]
    public long MaxUploadBytes { get; set; } = 50 * 1024 * 1024;
}
