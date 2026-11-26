using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using SqlSugar;

namespace Sim.SqlSugar.Core.Numbering;

[CodeFirst]
[SugarTable("framework_number_sequence")]
[SugarIndex("uidx_framework_number_sequence_key", nameof(SequenceKey), OrderByType.Asc, true)]
public sealed class NumberSequenceEntity : Entity
{
    public NumberSequenceEntity()
    {
    }

    public NumberSequenceEntity(Guid id) : base(id)
    {
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string SequenceKey { get; set; } = string.Empty;

    [SugarColumn(Length = 128, IsNullable = false)]
    public string BusinessKey { get; set; } = string.Empty;

    [SugarColumn(Length = 512, IsNullable = false)]
    public string ScopeKey { get; set; } = string.Empty;

    public long LastValue { get; set; }
}
