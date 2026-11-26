using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.OpenPlatform.Application.Contracts;
using Sim.OpenPlatform.Application.Contracts.Dtos;
using Sim.OpenPlatform.Domain.Entities;
using Sim.OpenPlatform.Domain.Shared;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.OpenPlatform.Application.Services;

/// <summary>
/// 开放平台令牌端点 — 第三方系统使用 AppId/AppSecret 换取 Access Token。
/// 匿名可访问；所有签发尝试（成功/失败）均写入访问日志。
/// </summary>
public class TokenAppService : ApplicationService, IOpenPlatformTokenService
{
    private readonly ISqlSugarRepository<OpenPlatformApplication> _applicationRepository;
    private readonly ISqlSugarRepository<OpenPlatformAccessLog> _accessLogRepository;
    private readonly IOpenPlatformSecretProtector _secretProtector;
    private readonly IOpenPlatformTokenIssuer _tokenIssuer;
    private readonly IOpenPlatformScopeCatalog _scopeCatalog;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TokenAppService(
        ISqlSugarRepository<OpenPlatformApplication> applicationRepository,
        ISqlSugarRepository<OpenPlatformAccessLog> accessLogRepository,
        IOpenPlatformSecretProtector secretProtector,
        IOpenPlatformTokenIssuer tokenIssuer,
        IOpenPlatformScopeCatalog scopeCatalog,
        IHttpContextAccessor httpContextAccessor)
    {
        _applicationRepository = applicationRepository;
        _accessLogRepository = accessLogRepository;
        _secretProtector = secretProtector;
        _tokenIssuer = tokenIssuer;
        _scopeCatalog = scopeCatalog;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 使用 AppId/AppSecret 换取 Access Token（client_credentials 模式）。
    /// 校验应用状态、密钥与客户端 IP，所有签发尝试均写入访问日志。
    /// </summary>
    /// <param name="input">令牌请求参数：AppId、AppSecret 与 GrantType。</param>
    /// <returns>令牌响应：AccessToken、TokenType 与有效期（秒）。</returns>
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "openplatform-token")]
    public async Task<OpenPlatformTokenResponse> CreateAsync(OpenPlatformTokenRequest input)
    {
        var clientIp = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

        if (!string.Equals(input.GrantType, "client_credentials", StringComparison.OrdinalIgnoreCase))
        {
            await LogAsync(Guid.Empty, input.AppId, false, "UnsupportedGrantType", clientIp);
            throw new BusinessException(OpenPlatformErrorCodes.InvalidCredentials, "Unsupported grant type.");
        }

        var application = await _applicationRepository.GetAsync(a => a.AppId == input.AppId);

        if (application is null)
        {
            await LogAsync(Guid.Empty, input.AppId, false, "UnknownAppId", clientIp);
            throw new BusinessException(OpenPlatformErrorCodes.InvalidCredentials, "Invalid AppId or AppSecret.");
        }

        if (!application.IsActive)
        {
            await LogAsync(application.Id, application.AppId, false, "Disabled", clientIp);
            throw new BusinessException(OpenPlatformErrorCodes.InvalidCredentials, "Invalid AppId or AppSecret.");
        }

        if (!SecretMatches(application, input.AppSecret))
        {
            await LogAsync(application.Id, application.AppId, false, "SecretMismatch", clientIp);
            throw new BusinessException(OpenPlatformErrorCodes.InvalidCredentials, "Invalid AppId or AppSecret.");
        }

        if (!IsIpAllowed(application.AllowedIps, clientIp))
        {
            await LogAsync(application.Id, application.AppId, false, "IpNotAllowed", clientIp);
            throw new BusinessException(OpenPlatformErrorCodes.InvalidCredentials, "Invalid AppId or AppSecret.");
        }

        var scopes = DeserializeScopes(application.Scopes);
        var allowedScopes = (await _scopeCatalog.GetGrantableScopesAsync())
            .Select(scope => scope.Code)
            .ToHashSet(StringComparer.Ordinal);
        if (!OpenPlatformScopePolicy.AreGrantable(scopes, allowedScopes))
        {
            await LogAsync(application.Id, application.AppId, false, "InvalidScope", clientIp);
            throw new BusinessException(OpenPlatformErrorCodes.InvalidCredentials, "Invalid AppId or AppSecret.");
        }
        var (accessToken, expiresIn) = await _tokenIssuer.IssueAccessTokenAsync(
            application.Id,
            application.AppId,
            scopes);

        await LogAsync(application.Id, application.AppId, true, null, clientIp);

        return new OpenPlatformTokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn
        };
    }

    private static bool IsIpAllowed(string? allowedIps, string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(allowedIps))
            return true;
        if (string.IsNullOrWhiteSpace(clientIp))
            return false;

        try
        {
            var allowed = JsonSerializer.Deserialize<List<string>>(allowedIps) ?? new List<string>();
            if (allowed.Count == 0)
                return true;

            return allowed.Any(ip => string.Equals(ip.Trim(), clientIp, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            // 配置损坏时不能放开所有来源，按不允许处理。
            return false;
        }
    }

    private bool SecretMatches(OpenPlatformApplication application, string plainSecret)
    {
        try
        {
            var stored = _secretProtector.Unprotect(application.ProtectedAppSecret);
            var a = Encoding.UTF8.GetBytes(stored);
            var b = Encoding.UTF8.GetBytes(plainSecret ?? string.Empty);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch
        {
            return false;
        }
    }

    private async Task LogAsync(Guid applicationId, string appId, bool succeeded, string? reason, string? clientIp)
    {
        await _accessLogRepository.InsertAsync(new OpenPlatformAccessLog
        {
            ApplicationId = applicationId,
            AppId = (appId ?? string.Empty)[..Math.Min(appId?.Length ?? 0, 40)],
            Succeeded = succeeded,
            Reason = reason,
            ClientIp = clientIp
        });
    }

    private static List<string> DeserializeScopes(string? scopes)
    {
        if (string.IsNullOrWhiteSpace(scopes))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(scopes) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}
