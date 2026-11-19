namespace Sim.System.Domain.Shared.Permissions;

/// <summary>
/// System module permission constants
/// </summary>
public static class SystemPermissions
{
    /// <summary>
    /// Module configuration permissions
    /// </summary>
    public static class ModuleConfig
    {
        /// <summary>
        /// Permission to create module configurations
        /// </summary>
        public const string Create = "system:module:create";
        
        /// <summary>
        /// Permission to update module configurations
        /// </summary>
        public const string Update = "system:module:update";
        
        /// <summary>
        /// Permission to delete module configurations
        /// </summary>
        public const string Delete = "system:module:delete";
    }
    
    /// <summary>
    /// Route configuration permissions
    /// </summary>
    public static class RouteConfig
    {
        /// <summary>
        /// Permission to create route configurations
        /// </summary>
        public const string Create = "system:route:create";
        
        /// <summary>
        /// Permission to update route configurations
        /// </summary>
        public const string Update = "system:route:update";
        
        /// <summary>
        /// Permission to delete route configurations
        /// </summary>
        public const string Delete = "system:route:delete";
    }

    /// <summary>
    /// User administration permissions
    /// </summary>
    public static class User
    {
        /// <summary>
        /// Permission to query users
        /// </summary>
        public const string Query = "system:user:query";

        /// <summary>
        /// Permission to create users
        /// </summary>
        public const string Create = "system:user:create";

        /// <summary>
        /// Permission to update users
        /// </summary>
        public const string Update = "system:user:update";

        /// <summary>
        /// Permission to reset a user's password to the configured default
        /// </summary>
        public const string ResetPassword = "system:user:reset-password";

        /// <summary>
        /// Permission to delete users
        /// </summary>
        public const string Delete = "system:user:delete";
    }

    /// <summary>
    /// Refresh token administration permissions
    /// </summary>
    public static class Token
    {
        /// <summary>
        /// Permission to revoke every refresh token of a user
        /// </summary>
        public const string RevokeAll = "system:token:revoke-all";

        /// <summary>
        /// Permission to clean up expired refresh tokens
        /// </summary>
        public const string Cleanup = "system:token:cleanup";
    }

    /// <summary>
    /// Role administration permissions
    /// </summary>
    public static class Role
    {
        /// <summary>
        /// Permission to manage roles and their permissions
        /// </summary>
        public const string Manage = "system:role:manage";
    }

    /// <summary>
    /// Role-module assignment permissions
    /// </summary>
    public static class RoleModule
    {
        /// <summary>
        /// Permission to manage the modules assigned to a role
        /// </summary>
        public const string Manage = "system:role-module:manage";
    }

    /// <summary>
    /// Permission catalog administration permissions
    /// </summary>
    public static class Permission
    {
        /// <summary>
        /// Permission to manage permission catalog entries
        /// </summary>
        public const string Manage = "system:permission:manage";
    }
}
