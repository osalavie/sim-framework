using Microsoft.Extensions.Configuration;

namespace Sim.SqlSugar.Core.Options;

/// <summary>
/// Resolves database connection strings from the unified <c>DataBase:ConnectionConfigs</c>
/// configuration section, with fallback to the standard top-level
/// <c>ConnectionStrings</c> section for backward compatibility.
/// </summary>
/// <remarks>
/// This is the single entry point for all components (SqlSugar, Quartz, migrations, etc.)
/// that need a database connection string. By routing every consumer through this
/// resolver, the connection string only needs to be declared once in
/// <c>DataBase:ConnectionConfigs</c>.
/// </remarks>
public static class DatabaseConnectionStringResolver
{
    /// <summary>
    /// Resolves a connection string by <paramref name="configId"/> from
    /// <c>DataBase:ConnectionConfigs</c>.
    /// <para>
    /// Resolution order:
    /// 1. Match <c>DataBase:ConnectionConfigs[*].ConfigId</c> (case-insensitive) and
    ///    return its <c>ConnectionString</c>.
    /// 2. If no match, return the first entry's <c>ConnectionString</c>.
    /// 3. Fallback to <c>ConnectionStrings[configId]</c> for backward compatibility.
    /// </para>
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="configId">
    /// The logical config id to look up (default: <c>"Default"</c>).
    /// This matches the <c>ConfigId</c> field in <c>DataBase:ConnectionConfigs</c>
    /// and the key in the top-level <c>ConnectionStrings</c> section.
    /// </param>
    /// <returns>The connection string, or <c>null</c> if not found.</returns>
    public static string? Resolve(IConfiguration configuration, string configId = "Default")
    {
        // 1. Try DataBase:ConnectionConfigs — the single source of truth
        var configsSection = configuration.GetSection("DataBase:ConnectionConfigs");
        if (configsSection.Exists())
        {
            foreach (var entry in configsSection.GetChildren())
            {
                var id = entry.GetValue<string>("ConfigId");
                if (string.Equals(id, configId, StringComparison.OrdinalIgnoreCase))
                {
                    var connStr = entry.GetValue<string>("ConnectionString");
                    if (!string.IsNullOrWhiteSpace(connStr))
                    {
                        return connStr;
                    }
                }
            }

            // No exact match — use the first config as fallback
            var first = configsSection.GetChildren().FirstOrDefault();
            if (first != null)
            {
                var connStr = first.GetValue<string>("ConnectionString");
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    return connStr;
                }
            }
        }

        // 2. Backward compatibility: top-level ConnectionStrings
        return configuration.GetConnectionString(configId);
    }
}
