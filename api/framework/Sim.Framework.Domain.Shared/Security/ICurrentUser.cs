using System.Security.Claims;

namespace Sim.Framework.Domain.Shared.Security;

/// <summary>
/// Current user abstraction. Replaces Volo.Abp.Users.ICurrentUser.
/// Provides access to the authenticated user's claims from the current HTTP context.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? Id { get; }
    string? UserName { get; }
    string? Name { get; }
    string? SurName { get; }
    string? Email { get; }
    bool EmailVerified { get; }
    string? PhoneNumber { get; }
    bool PhoneNumberVerified { get; }
    Guid? TenantId { get; }
    string[] Roles { get; }

    Claim? FindClaim(string claimType);
    string? FindClaimValue(string claimType);
    Claim[] FindClaims(string claimType);
    Claim[] GetAllClaims();
    bool IsInRole(string roleName);
}
