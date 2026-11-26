namespace Sim.OpenPlatform.Application.Contracts;

/// <summary>
/// 开放平台访问令牌签发端口。
/// 定义在 Contracts 层，实现在组合根（Sim.Admin.Domain），
/// 复用现有 JWT 签名体系为第三方应用签发限定权限范围的 Access Token。
/// </summary>
public interface IOpenPlatformTokenIssuer
{
    Task<(string AccessToken, int ExpiresIn)> IssueAccessTokenAsync(
        Guid applicationId,
        string appId,
        List<string> scopes);
}