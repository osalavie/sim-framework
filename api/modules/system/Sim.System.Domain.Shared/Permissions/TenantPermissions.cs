namespace Sim.System.Domain.Shared.Permissions;

/// <summary>
/// Tenant administration permission codes.
/// </summary>
public static class TenantPermissions
{
    /// <summary>
    /// Permission to create, update, enable, disable and delete tenants.
    /// </summary>
    public const string Manage = "system:tenant:manage";
}
