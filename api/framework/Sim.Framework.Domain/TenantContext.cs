namespace Sim.Framework.Domain;

/// <summary>
/// Per-request tenant context for applying SqlSugar multi-tenant query filters.
/// The value flows with the async execution context and is set by the multi-tenancy middleware.
/// </summary>
public static class TenantContext
{
    private static readonly AsyncLocal<Guid?> _currentTenantId = new();

    /// <summary>
    /// Current tenant ID for the executing request. Null = host (no filter applied).
    /// </summary>
    public static Guid? CurrentTenantId
    {
        get => _currentTenantId.Value;
        set => _currentTenantId.Value = value;
    }
}
