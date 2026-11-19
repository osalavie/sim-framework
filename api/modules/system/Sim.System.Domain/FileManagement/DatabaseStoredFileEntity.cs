using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.System.Domain.FileManagement;

[SugarTable("system_stored_file")]
[SugarIndex("idx_system_stored_file_key", nameof(StorageKey), OrderByType.Asc, true)]
public sealed class DatabaseStoredFileEntity : AuditedAggregateRoot
{
    public DatabaseStoredFileEntity()
    {
    }

    public DatabaseStoredFileEntity(Guid id) : base(id)
    {
    }

    [SugarColumn(Length = 512, IsNullable = false)]
    public string StorageKey { get; set; } = string.Empty;

    [SugarColumn(Length = 255, IsNullable = false)]
    public string FileName { get; set; } = string.Empty;

    [SugarColumn(Length = 128, IsNullable = false)]
    public string ContentType { get; set; } = "application/octet-stream";

    [SugarColumn(IsNullable = false)]
    public long Length { get; set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Sha256 { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public byte[] Content { get; set; } = [];
}
