namespace Sim.OpenPlatform.Application.Contracts.Dtos;

/// <summary>
/// 开放平台令牌请求参数 — 第三方系统以客户端凭证（AppId/AppSecret）换取 Access Token。
/// </summary>
public class OpenPlatformTokenRequest
{
    /// <summary>第三方应用 AppId（对应 OAuth2 的 client_id）。</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>第三方应用 AppSecret（对应 OAuth2 的 client_secret）。</summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>授权类型，固定为 client_credentials（对应 OAuth2 的 grant_type）。</summary>
    public string GrantType { get; set; } = "client_credentials";
}

/// <summary>
/// 开放平台令牌响应 — 包含可调用的 Access Token 及其有效期。
/// </summary>
public class OpenPlatformTokenResponse
{
    /// <summary>访问令牌，后续请求需在 Authorization 头中以 Bearer 方式携带。</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>令牌类型，固定为 Bearer。</summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>令牌有效期（秒）。</summary>
    public int ExpiresIn { get; set; }
}