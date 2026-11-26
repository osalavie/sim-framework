using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.User;

public interface IExternalLoginService : IApplicationService
{
    /// <summary>
    /// 获取已启用的外部登录提供方。
    /// </summary>
    Task<IReadOnlyList<ExternalLoginProviderDto>> GetProvidersAsync();

    /// <summary>
    /// 创建外部登录跳转地址与一次性防伪造状态。
    /// </summary>
    Task<ExternalLoginRedirectDto> GetRedirectAsync(string providerId);

    /// <summary>
    /// 统一处理外部身份回调：根据加密 state 判断登录或绑定。未绑定身份返回注册模式；已绑定身份登录；state 包含绑定用户时为当前用户绑定。
    /// </summary>
    Task<LoginUserDto> GetCallbackAsync(
        string providerId,
        [FromQuery] ExternalLoginCallbackRequest input);

    /// <summary>
    /// 外部登录注册（未绑定的外部账号先填表注册本系统账号再绑定）。
    /// </summary>
    Task<LoginUserDto> RegisterWithExternalLoginAsync(
        ExternalLoginRegistrationRequest input);

    /// <summary>
    /// 外部登录关联（使用已有的本系统账号密码验证后，把外部身份绑定到该账号）。
    /// </summary>
    Task<LoginUserDto> LinkWithExternalLoginAsync(
        LinkWithExternalLoginRequest input);

    /// <summary>
    /// 获取当前用户的外部登录绑定列表。
    /// </summary>
    Task<IReadOnlyList<ExternalLoginBindingDto>> GetMyBindingsAsync();

    /// <summary>
    /// 获取当前用户绑定外部登录的跳转地址。
    /// </summary>
    Task<ExternalLoginRedirectDto> GetMyBindingRedirectAsync(string providerId);

    /// <summary>
    /// 解绑外部登录。
    /// </summary>
    Task DeleteMyBindingAsync(string providerId);
}

public sealed class ExternalLoginProviderDto
{
    public required string Provider { get; init; }

    public required string DisplayName { get; init; }
}

public sealed class ExternalLoginRedirectDto
{
    public required string Provider { get; init; }

    public required string RedirectUrl { get; init; }

    public required string CallbackUrl { get; init; }

    public required string State { get; init; }
}

public sealed class ExternalLoginCallbackRequest
{
    public string? Code { get; set; }

    public string? State { get; set; }

    public string? Error { get; set; }

    public string? ErrorDescription { get; set; }
}

/// <summary>
/// 外部登录注册请求。
/// </summary>
public sealed class ExternalLoginRegistrationRequest
{
    /// <summary>注册令牌（从回调返回中获取）。</summary>
    public required string RegistrationToken { get; init; }

    /// <summary>用户名。</summary>
    public required string UserName { get; init; }

    /// <summary>密码。</summary>
    public required string Password { get; init; }

    /// <summary>昵称。</summary>
    public string? NickName { get; init; }

    /// <summary>邮箱；为空时沿用第三方身份携带的邮箱。</summary>
    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; init; }

    /// <summary>手机号（选填）。</summary>
    [StringLength(20)]
    public string? Phone { get; init; }
}

/// <summary>
/// 外部登录关联已有账号请求。
/// </summary>
public sealed class LinkWithExternalLoginRequest
{
    /// <summary>注册令牌（从回调返回中获取）。</summary>
    public required string RegistrationToken { get; init; }

    /// <summary>本系统账号用户名。</summary>
    public required string UserName { get; init; }

    /// <summary>本系统账号密码。</summary>
    public required string Password { get; init; }
}

/// <summary>
/// 外部登录绑定信息。
/// </summary>
public sealed class ExternalLoginBindingDto
{
    public required string Provider { get; init; }

    public required string DisplayName { get; init; }

    public string? ProviderUserName { get; set; }

    public string? Email { get; set; }

    public DateTime CreationTime { get; set; }
}
