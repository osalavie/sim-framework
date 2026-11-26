namespace Sim.System.Domain.Shared.Consts;

/// <summary>
/// System module constants
/// </summary>
public static class SystemConsts
{
    /// <summary>
    /// Database table prefix for system module entities.
    /// The global <c>sim_</c> prefix (defined in <see cref="Sim.Framework.Domain.Shared.Consts.TablePrefix"/>)
    /// is applied automatically by <c>SqlSugarCoreModule</c>'s EntityNameService.
    /// Final table name: <c>sim_system_&lt;entity&gt;</c>
    /// </summary>
    public const string DbTablePrefix = "system_";
    
    /// <summary>
    /// Layout type constants
    /// </summary>
    public static class LayoutTypes
    {
        /// <summary>
        /// Admin layout - routes become children of AdminLayout
        /// </summary>
        public const string Admin = "admin";
        
        /// <summary>
        /// Custom layout - routes use custom layout component
        /// </summary>
        public const string Custom = "custom";
        
        /// <summary>
        /// No layout - routes are registered at top level
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// All valid layout types
        /// </summary>
        public static readonly string[] ValidTypes = { Admin, Custom, None };

        /// <summary>
        /// Check if a layout type is valid
        /// </summary>
        public static bool IsValid(string layoutType)
        {
            return ValidTypes.Contains(layoutType, StringComparer.OrdinalIgnoreCase);
        }
    }
}
