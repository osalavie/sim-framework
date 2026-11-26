using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sim.Framework.Domain.Shared.Security;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.Security;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? Id => TryReadGuid(TokenTypeConst.Id);

    public string? UserName => FindClaimValue(TokenTypeConst.UserName);

    public string? Name => FindClaimValue(TokenTypeConst.Name) ?? UserName;

    public string? SurName => FindClaimValue(TokenTypeConst.SurName);

    public string? Email => FindClaimValue(TokenTypeConst.Email);

    public bool EmailVerified => FindClaimValue("email_verified") == "true";

    public string? PhoneNumber => FindClaimValue(TokenTypeConst.Phone);

    public bool PhoneNumberVerified => FindClaimValue("phone_number_verified") == "true";

    public string[] Roles => FindClaims(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

    public Guid? TenantId => TryReadGuid(TokenTypeConst.TenantId);

    public Claim? FindClaim(string claimType) => Principal?.FindFirst(claimType);

    public string? FindClaimValue(string claimType) => FindClaim(claimType)?.Value;

    public Claim[] FindClaims(string claimType) => Principal?.FindAll(claimType).ToArray() ?? [];

    public Claim[] GetAllClaims() => Principal?.Claims.ToArray() ?? [];

    public bool IsInRole(string roleName) => Principal?.IsInRole(roleName) == true;

    private Guid? TryReadGuid(string claimType) =>
        Guid.TryParse(FindClaimValue(claimType), out var value) ? value : null;
}
