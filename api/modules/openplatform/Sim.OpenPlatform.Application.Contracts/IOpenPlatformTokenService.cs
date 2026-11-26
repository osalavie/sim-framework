using Sim.Framework.Application.Contracts.IServices;
using Sim.OpenPlatform.Application.Contracts.Dtos;

namespace Sim.OpenPlatform.Application.Contracts;

/// <summary>
/// 开放平台令牌端点 — 第三方系统使用 AppId/AppSecret 换取 Access Token。
/// 该接口匿名可访问，由内部逻辑完成凭证校验与审计记录。
/// </summary>
public interface IOpenPlatformTokenService : IApplicationService
{
    /// <summary>
    /// 使用 AppId/AppSecret 换取 Access Token（client_credentials 模式）。
    /// 该接口匿名可访问，校验失败时会抛出业务异常并记录审计日志。
    /// </summary>
    /// <param name="input">令牌请求参数：AppId、AppSecret 与 GrantType。</param>
    /// <returns>令牌响应：AccessToken、TokenType 与有效期（秒）。</returns>
    Task<OpenPlatformTokenResponse> CreateAsync(OpenPlatformTokenRequest input);
}