using System.Security.Claims;

namespace Sim.Framework.Application.Services;

/// <summary>
/// Null implementation of ICurrentUser for cases where no user context is available.
/// </summary>
internal class NullCurrentUser : Sim.Framework.Domain.Shared.Security.ICurrentUser
{
    public static NullCurrentUser Instance { get; } = new();

    public bool IsAuthenticated => false;
    public Guid? Id => null;
    public string? UserName => null;
    public string? Name => null;
    public string? SurName => null;
    public string? Email => null;
    public bool EmailVerified => false;
    public string? PhoneNumber => null;
    public bool PhoneNumberVerified => false;
    public Guid? TenantId => null;
    public string[] Roles => [];

    public Claim? FindClaim(string claimType) => null;
    public string? FindClaimValue(string claimType) => null;
    public Claim[] FindClaims(string claimType) => [];
    public Claim[] GetAllClaims() => [];
    public bool IsInRole(string roleName) => false;
}
