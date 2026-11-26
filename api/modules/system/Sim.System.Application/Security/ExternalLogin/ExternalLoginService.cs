using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.System.Application.Contracts.User;
using Sim.System.Application.Security;
using Sim.System.Domain.Shared;
using Sim.SqlSugar.Core.Repositories;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.ExternalLogin;
using Sim.System.Domain.SystemConfig;

namespace Sim.System.Application.Security.ExternalLogin;

public class ExternalLoginService(
    IExternalLoginProviderRegistry providerRegistry,
    IExternalLoginStateService stateService,
    IExternalLoginAccountService accountService,
    IExternalLoginRepository externalLoginRepository,
    ISqlSugarRepository<SystemConfigurationItemEntity> configurationRepository,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<ExternalLoginOptions> externalLoginOptions,
    IHttpContextAccessor httpContextAccessor)
    : ApplicationService, IExternalLoginService
{
    private const string RegistrationProtectorName = "Sim.System.ExternalLogin.Registration.v1";

    /// <summary>
    /// 获取已启用的外部登录提供方列表。
    /// </summary>
    [AllowAnonymous]
    public Task<IReadOnlyList<ExternalLoginProviderDto>> GetProvidersAsync()
    {
        return Task.FromResult<IReadOnlyList<ExternalLoginProviderDto>>(
            providerRegistry.EnabledProviders
                .Select(provider => new ExternalLoginProviderDto
                {
                    Provider = provider.Id,
                    DisplayName = provider.DisplayName
                })
                .ToList());
    }

    /// <summary>
    /// 获取指定外部登录提供方的授权跳转地址。
    /// </summary>
    [AllowAnonymous]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "external-login")]
    public async Task<ExternalLoginRedirectDto> GetRedirectAsync(string providerId)
    {
        var provider = providerRegistry.Get(providerId);
        var state = await stateService.BeginAsync(provider.Id);
        var redirectUrl = await provider.BuildRedirectUrlAsync(
            new ExternalLoginRedirectContext(state.CallbackUrl, state.State, state.Nonce));
        return new ExternalLoginRedirectDto
        {
            Provider = provider.Id,
            RedirectUrl = redirectUrl,
            CallbackUrl = state.CallbackUrl,
            State = state.State
        };
    }

    /// <summary>
    /// 处理外部登录回调：完成登录，或返回用于注册/绑定的一次性令牌。
    /// </summary>
    [AllowAnonymous]
    [SqlSugarUnitOfWork]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "external-login")]
    public async Task<LoginUserDto> GetCallbackAsync(
        string providerId,
        [FromQuery] ExternalLoginCallbackRequest input)
    {
        var provider = providerRegistry.Get(providerId);
        var state = await stateService.ConsumeAsync(provider.Id, input.State);

        if (!string.IsNullOrWhiteSpace(input.Error))
        {
            throw new BusinessException(
                SystemErrorCodes.ExternalLoginProviderError,
                "The external login provider rejected the request.");
        }

        var query = httpContextAccessor.HttpContext?.Request.Query
            ?? throw new InvalidOperationException("External login requires an HTTP context.");
        var queryMap = query
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value.ToString());
        var identity = await provider.AuthenticateAsync(
            new ExternalLoginAuthenticationContext(input, state.CallbackUrl, state.Nonce, queryMap));
        var providerKey = ExternalLoginSecurity.CreateProviderKeyHash(provider.Id, identity.ProviderKey);

        if (state.BindingUserId != null)
        {
            return await CompleteBindingAsync(provider, identity, providerKey, state.BindingUserId.Value);
        }

        var binding = await externalLoginRepository.GetAsync(provider.Id, providerKey);
        if (binding == null)
        {
            // 未绑定第三方身份：返回一次性注册令牌，由前端在“关联已有账号”与
            // “注册新账号”之间选择。注册开关只约束新建账号，不影响关联已有账号。
            return new LoginUserDto
            {
                RequiresExternalLoginRegistration = true,
                ExternalLoginRegistrationAllowed = await IsSelfRegistrationEnabledAsync(),
                ExternalLoginProvider = provider.Id,
                ExternalLoginProviderDisplayName = provider.DisplayName,
                ExternalLoginUserName = identity.UserName,
                ExternalLoginDisplayName = identity.DisplayName,
                ExternalLoginEmail = identity.Email,
                ExternalLoginRegistrationToken = CreateRegistrationToken(provider.Id, identity)
            };
        }

        return await accountService.LoginAsync(provider.Id, identity);
    }

    private async Task<LoginUserDto> CompleteBindingAsync(
        IExternalLoginProvider provider,
        ExternalLoginIdentity identity,
        string providerKey,
        Guid bindingUserId)
    {
        var currentUserId = CurrentUser.Id ?? throw new AuthorizationException();
        if (currentUserId != bindingUserId)
        {
            throw new AuthorizationException();
        }

        var existingBinding = await externalLoginRepository.GetAsync(provider.Id, providerKey);
        if (existingBinding != null)
        {
            if (existingBinding.UserId == bindingUserId)
            {
                return new LoginUserDto
                {
                    Id = bindingUserId,
                    UserName = string.Empty,
                    ExternalLoginBindingCompleted = true
                };
            }

            throw new BusinessException(
                SystemErrorCodes.ExternalLoginProviderError,
                "This external account has already been linked to another user.");
        }

        var binding = new ExternalLoginEntity
        {
            Id = Guid.NewGuid(),
            UserId = bindingUserId,
            Provider = provider.Id,
            ProviderKey = providerKey,
            ProviderUserName = identity.UserName,
            Email = identity.Email
        };
        await externalLoginRepository.InsertAsync(binding);

        return new LoginUserDto
        {
            Id = bindingUserId,
            UserName = string.Empty,
            ExternalLoginBindingCompleted = true
        };
    }

    /// <summary>
    /// 使用外部登录令牌注册新账号并完成登录。
    /// </summary>
    [AllowAnonymous]
    [SqlSugarUnitOfWork]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "external-login")]
    public async Task<LoginUserDto> RegisterWithExternalLoginAsync(
        ExternalLoginRegistrationRequest input)
    {
        // 令牌签发后管理员可能关闭注册，补齐时再次校验开关
        if (!await IsSelfRegistrationEnabledAsync())
        {
            throw new BusinessException(
                SystemErrorCodes.ExternalLoginRegistrationDisabled,
                "Self-registration is disabled on this site.");
        }

        var payload = DecryptRegistrationToken(input.RegistrationToken);

        // 委托 UserService 完成用户创建 + 绑定 + 登录
        return await accountService.RegisterWithExternalLoginAsync(
            payload.Provider,
            CreateIdentity(payload),
            input.UserName,
            input.Password,
            input.NickName,
            input.Email,
            input.Phone);
    }

    /// <summary>
    /// 将外部登录令牌绑定到已有账号并完成登录。
    /// </summary>
    [AllowAnonymous]
    [SqlSugarUnitOfWork]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "external-login")]
    public async Task<LoginUserDto> LinkWithExternalLoginAsync(LinkWithExternalLoginRequest input)
    {
        var payload = DecryptRegistrationToken(input.RegistrationToken);

        // 关联已有账号不创建新用户，因此不受注册开关约束。
        return await accountService.LinkWithExternalLoginAsync(
            payload.Provider,
            CreateIdentity(payload),
            input.UserName,
            input.Password);
    }

    /// <summary>
    /// 获取当前用户的外部登录绑定列表。
    /// </summary>
    [Authorize]
    [SqlSugarUnitOfWork]
    public async Task<IReadOnlyList<ExternalLoginBindingDto>> GetMyBindingsAsync()
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var bindings = await externalLoginRepository.GetListByUserIdAsync(userId);
        var providerNames = providerRegistry.EnabledProviders
            .ToDictionary(p => p.Id, p => p.DisplayName, StringComparer.OrdinalIgnoreCase);

        return bindings.Select(b => new ExternalLoginBindingDto
        {
            Provider = b.Provider,
            DisplayName = providerNames.GetValueOrDefault(b.Provider, b.Provider),
            ProviderUserName = b.ProviderUserName,
            Email = b.Email,
            CreationTime = b.CreationTime
        }).ToList();
    }

    /// <summary>
    /// 获取当前用户绑定指定外部登录提供方的授权跳转地址。
    /// </summary>
    [Authorize]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "external-login")]
    public async Task<ExternalLoginRedirectDto> GetMyBindingRedirectAsync(string providerId)
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var provider = providerRegistry.Get(providerId);
        var state = await stateService.BeginBindingAsync(provider.Id, userId);
        var redirectUrl = await provider.BuildRedirectUrlAsync(
            new ExternalLoginRedirectContext(state.CallbackUrl, state.State, state.Nonce));
        return new ExternalLoginRedirectDto
        {
            Provider = provider.Id,
            RedirectUrl = redirectUrl,
            CallbackUrl = state.CallbackUrl,
            State = state.State
        };
    }

    /// <summary>
    /// 解除当前用户与指定外部登录提供方的绑定。
    /// </summary>
    [Authorize]
    [SqlSugarUnitOfWork]
    public async Task DeleteMyBindingAsync(string providerId)
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var bindings = await externalLoginRepository.GetListByUserIdAsync(userId);
        var binding = bindings.FirstOrDefault(b =>
            string.Equals(b.Provider, providerId, StringComparison.OrdinalIgnoreCase));
        if (binding == null)
        {
            throw new BusinessException(
                SystemErrorCodes.ExternalLoginProviderError,
                "The external login binding was not found.");
        }

        await externalLoginRepository.DeleteAsync(binding.Id);
    }

    /// <summary>
    /// 读取站点配置 security.registration-enabled（默认关闭）。
    /// 未绑定的第三方身份只有在该开关开放时才允许进入自助注册补齐流程。
    /// </summary>
    private async Task<bool> IsSelfRegistrationEnabledAsync()
    {
        var tenantScope = SystemConfigurationItemEntity.CurrentTenantScope();
        var configuration = await configurationRepository.GetAsync(item =>
            item.TenantScope == tenantScope &&
            item.ConfigKey == SystemConfigurationKeys.RegistrationEnabled &&
            item.Enabled);
        return bool.TryParse(configuration?.Value, out var enabled) && enabled;
    }

    private RegistrationTokenPayload DecryptRegistrationToken(string token)
    {
        RegistrationTokenPayload payload;
        try
        {
            var protector = dataProtectionProvider.CreateProtector(RegistrationProtectorName);
            var json = protector.Unprotect(token);
            payload = JsonSerializer.Deserialize<RegistrationTokenPayload>(json)
                ?? throw new InvalidOperationException();
        }
        catch
        {
            throw new BusinessException(
                SystemErrorCodes.InvalidExternalLoginState,
                "The registration token is invalid or expired.");
        }

        if (payload.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BusinessException(
                SystemErrorCodes.InvalidExternalLoginState,
                "The registration token has expired.");
        }

        return payload;
    }

    private static ExternalLoginIdentity CreateIdentity(RegistrationTokenPayload payload) =>
        new(payload.ProviderKey, payload.UserName, payload.DisplayName, payload.Email);

    private string CreateRegistrationToken(string provider, ExternalLoginIdentity identity)
    {
        var protector = dataProtectionProvider.CreateProtector(RegistrationProtectorName);
        var payload = new RegistrationTokenPayload
        {
            Provider = provider,
            ProviderKey = identity.ProviderKey,
            UserName = identity.UserName,
            DisplayName = identity.DisplayName,
            Email = identity.Email,
            ExpiresAt = DateTime.UtcNow.AddMinutes(externalLoginOptions.Value.StateExpirationMinutes)
        };
        return protector.Protect(JsonSerializer.Serialize(payload));
    }

    private sealed class RegistrationTokenPayload
    {
        public string Provider { get; set; } = string.Empty;
        public string ProviderKey { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
