using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Sim.OpenPlatform.Application.Contracts;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Security;

namespace Sim.Admin.Domain.Services;

/// <summary>
/// 开放平台访问令牌签发器 — 实现在组合根，复用 System 模块的 JWT 签名体系，
/// 为第三方应用签发限定授权范围（Permission Claim）的 Access Token。
/// </summary>
public class OpenPlatformTokenIssuer : IOpenPlatformTokenIssuer
{
    private readonly ISecurityManager _securityManager;
    private readonly IConfiguration _configuration;

    public OpenPlatformTokenIssuer(
        ISecurityManager securityManager,
        IConfiguration configuration)
    {
        _securityManager = securityManager;
        _configuration = configuration;
    }

    public Task<(string AccessToken, int ExpiresIn)> IssueAccessTokenAsync(
        Guid applicationId,
        string appId,
        List<string> scopes)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"openplatform:{appId}"),
            new("client_id", appId),
            new("application_id", applicationId.ToString()),
            new("grant_type", "client_credentials")
        };

        foreach (var scope in scopes.Distinct(StringComparer.Ordinal))
        {
            claims.Add(new Claim(TokenTypeConst.Permission, scope));
        }

        var accessToken = _securityManager.GenerateAccessToken(claims);
        var expiresIn = _configuration.GetValue<int>("JwtSettings:AccessTokenExpirationMinutes", 10) * 60;

        return Task.FromResult((accessToken, expiresIn));
    }
}