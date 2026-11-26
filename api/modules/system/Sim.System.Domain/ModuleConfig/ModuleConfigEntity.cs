using Sim.SqlSugar.Core.Attributes;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Exceptions;
using SqlSugar;
using Sim.Framework.Domain.Entities;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Domain.ModuleConfig;

/// <summary>
/// Module configuration entity (Aggregate Root)
/// </summary>
[CodeFirst]
[SugarTable(SystemConsts.DbTablePrefix + "module_config")]
[SugarIndex($"idx_{SystemConsts.DbTablePrefix}module_config_name", nameof(Name), OrderByType.Asc)]
public class ModuleConfigEntity : AuditedAggregateRoot
{
    /// <summary>
    /// Module name
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = false)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Module description
    /// </summary>
    [SugarColumn(Length = 512, IsNullable = true)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Module version
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = true)]
    public string? Version { get; set; }
    
    /// <summary>
    /// Whether the module is enabled
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool Enabled { get; set; }
    
    /// <summary>
    /// Module icon
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Display order
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int Order { get; set; }
    
    private string _layout = SystemConsts.LayoutTypes.Admin;

    /// <summary>
    /// Layout type: "admin", "custom", or "none"
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Layout
    {
        get => _layout;
        set
        {
            if (!SystemConsts.LayoutTypes.IsValid(value))
            {
                throw new InvalidModuleDataException(
                    "Layout",
                    $"Layout type must be one of: {string.Join(", ", SystemConsts.LayoutTypes.ValidTypes)}"
                );
            }
            _layout = value;
        }
    }
    
    /// <summary>
    /// Empty redirect path
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = true)]
    public string? EmptyRedirect { get; set; }
}
