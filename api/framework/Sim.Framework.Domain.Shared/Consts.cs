namespace Sim.Framework.Domain.Shared;

/// <summary>
/// Framework-level constants shared across all modules.
/// </summary>
public static class Consts
{
    /// <summary>
    /// Global database table prefix applied to all entities by
    /// <c>SqlSugarCoreModule</c>'s <c>EntityNameService</c>.
    /// Module-specific prefixes (e.g. <c>system_</c>, <c>approval_</c>) are set
    /// via <c>[SugarTable]</c> and this prefix is prepended automatically.
    /// Final table name pattern: <c>sim_&lt;module&gt;_&lt;entity&gt;</c>.
    /// </summary>
    public const string TablePrefix = "sim_";
}
