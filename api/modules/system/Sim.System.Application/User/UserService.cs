using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Sim.System.Application.Contracts.User;
using System.Security.Claims;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Domain.Shared.Security;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared.Consts;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.System.Domain.Shared;
using System.Security.Cryptography;
using System.Text;
using Sim.System.FileManagement;
using Sim.Framework.Application.Contracts.Content;
using Sim.System.Application.Security;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Application.SystemConfig;
using Sim.System.Domain.E2ee;
using Sim.System.Domain.ExternalLogin;
using Sim.System.Domain.Permission;
using Sim.System.Domain.Role;
using Sim.System.Domain.Security;
using Sim.System.Domain.User;

namespace Sim.System.Application.User;

[Authorize]
public class UserService(
    ISecurityManager passwordManager,
    IUserRepository userRepository,
    IUserRoleRepository userRoleRepository,
    IRolePermissionRepository rolePermissionRepository,
    IPermissionRepository permissionRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IRoleRepository roleRepository,
    IHttpContextAccessor httpContextAccessor,
    IAntiforgery antiforgery,
    IOptions<SecurityOptions> securityOptions,
    IAvatarStorage avatarStorage,
    IVerificationMessageSender verificationMessageSender,
    IUserSecuritySecretProtector secretProtector,
    IExternalLoginRepository externalLoginRepository,
    ISystemConfigurationReader systemConfigurationReader,
    IUserSessionRevoker sessionRevoker,
    ITenantStatusValidator tenantStatusValidator,
    IAuthorizationService authorizationService,
    ISqlSugarRepository<UserPreferenceEntity> userPreferenceRepository,
    ISqlSugarRepository<UserE2eeIdentityEntity> userE2eeIdentityRepository,
    IConfiguration configuration,
    ILogger<UserService> logger,
    IEnumerable<IUserCreatedHandler>? userCreatedHandlers = null,
    ISqlSugarRepository<UserE2eePasskeyEntity>? userE2eePasskeys = null,
    ISqlSugarRepository<UserE2eePasskeyChallengeEntity>? userE2eeChallenges = null,
    IEnumerable<IUserDeletedHandler>? userDeletedHandlers = null)
    : CrudAppService<Guid, UserEntity, UserDto, CreateUserDto, UpdateUserDto>(userRepository), IUserService, IUserPublicService, IExternalLoginAccountService
{
    /// <summary>
    /// 获取当前用户的个人资料。
    /// </summary>
    [HttpGet]
    public async Task<UserDto> GetMyProfileAsync()
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var user = await userRepository.GetAsync(userId)
            ?? throw new BusinessException(SystemErrorCodes.UserNotFound, "User not found.");
        return await MapToDtoAsync(user);
    }

    /// <summary>
    /// 更新当前用户的昵称、姓名与个人说明。
    /// </summary>
    [HttpPut]
    public async Task<UserDto> UpdateMyProfileAsync(UpdateMyProfileDto input)
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var user = await userRepository.GetAsync(userId)
            ?? throw new BusinessException(SystemErrorCodes.UserNotFound, "User not found.");

        var nickName = NormalizeRequiredNickName(input.NickName);
        await EnsureNickNameIsUniqueAsync(nickName, userId);

        user.NickName = nickName;
        user.Name = Normalize(input.Name);
        user.Description = Normalize(input.Description);

        await userRepository.UpdateAsync(user);
        return await MapToDtoAsync(user);
    }

    /// <summary>
    /// 上传并更新当前用户的头像。
    /// </summary>
    [HttpPost]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "avatar-upload")]
    public async Task<UserDto> UploadMyAvatarAsync(
        IRemoteStreamContent file,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var user = await userRepository.GetAsync(userId)
            ?? throw new BusinessException(SystemErrorCodes.UserNotFound, "User not found.");
        try
        {
            using (file)
            {
                var stored = await avatarStorage.SaveAsync(
                    "users",
                    userId,
                    file.GetStream(),
                    cancellationToken);
                user.Avatar = stored.Url;
            }
        }
        catch (InvalidAvatarFileException)
        {
            throw new BusinessException(
                SystemErrorCodes.InvalidAvatar,
                "The uploaded avatar file is invalid.");
        }

        await userRepository.UpdateAsync(user);
        return await MapToDtoAsync(user);
    }

    /// <summary>
    /// 批量获取用户头像地址（单次最多 100 个用户）。
    /// </summary>
    [AllowAnonymous]
    [RateLimit(permitLimit: 60, windowSeconds: 60, PartitionName = "avatar-batch")]
    public async Task<IReadOnlyDictionary<Guid, string?>> GetAvatarsAsync(IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        if (userIds.Count > 100)
        {
            throw new BusinessException(
                SystemErrorCodes.AvatarBatchTooLarge,
                "At most 100 user ids can be queried at once.");
        }

        var ids = userIds.Distinct().ToList();
        var rows = await userRepository.GetListAsync(u => ids.Contains(u.Id));
        return rows.ToDictionary(u => u.Id, u => u.Avatar);
    }

    /// <summary>
    /// 批量获取用户显示名称（单次最多 200 个用户）。
    /// </summary>
    [HttpGet]
    [RateLimit(permitLimit: 120, windowSeconds: 60, PartitionName = "display-name-batch")]
    public async Task<IReadOnlyDictionary<Guid, UserDisplayNameDto>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserDisplayNameDto>();
        }

        if (userIds.Count > 200)
        {
            throw new BusinessException(
                SystemErrorCodes.DisplayNameBatchTooLarge,
                "At most 200 user ids can be queried at once.");
        }

        var ids = userIds.Distinct().ToList();
        var rows = await userRepository.GetListAsync(u => ids.Contains(u.Id));
        return rows.ToDictionary(
            u => u.Id,
            u => new UserDisplayNameDto
            {
                DisplayName = string.IsNullOrWhiteSpace(u.NickName) ? u.UserName : u.NickName,
                UserName = u.UserName,
            });
    }

    /// <summary>
    /// 检查昵称是否可用。
    /// </summary>
    [HttpGet]
    [RateLimit(permitLimit: 60, windowSeconds: 60, PartitionName = "nickname-availability")]
    public async Task<bool> GetNickNameAvailabilityAsync(
        string? nickName,
        Guid? excludeUserId = null)
    {
        var normalized = Normalize(nickName);
        if (normalized == null)
        {
            return false;
        }

        var hasExcludedUser = excludeUserId.HasValue;
        var excludedId = excludeUserId.GetValueOrDefault();
        var existing = hasExcludedUser
            ? await userRepository.GetAsync(user => user.NickName == normalized && user.Id != excludedId)
            : await userRepository.GetAsync(user => user.NickName == normalized);
        return existing == null;
    }

    /// <summary>
    /// 获取指定用户的详情（含角色）。
    /// </summary>
    [Permission(SystemPermissions.User.Query)]
    public override async Task<UserDto> GetAsync(Guid id)
    {
        var dto = await base.GetAsync(id);
        await FillRolesAsync([dto]);
        return dto;
    }

    /// <summary>
    /// 分页获取用户列表（含角色）。
    /// </summary>
    [Permission(SystemPermissions.User.Query)]
    public override async Task<PagedResultDto<UserDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var result = await base.GetListAsync(input);
        await FillRolesAsync(result.Items);
        return result;
    }

    /// <summary>
    /// 创建用户，可同时分配角色。
    /// </summary>
    [Permission(SystemPermissions.User.Create)]
    [SqlSugarUnitOfWork]
    public override async Task<UserDto> CreateAsync(CreateUserDto input)
    {
        input.NickName = NormalizeRequiredNickName(input.NickName);
        input.Email = Normalize(input.Email);
        await EnsureUserIsUniqueAsync(input.UserName, input.Email);
        await EnsureNickNameIsUniqueAsync(input.NickName);

        var defaultPassword = await GetDefaultUserPasswordAsync();
        UserEntity user = await MapToEntityAsync(input);
        SetPassword(user, defaultPassword);

        await Repository.InsertAsync(user);

        if (input.RoleIds is { Count: > 0 })
        {
            await EnsureCanAssignRolesAsync();
            await ReplaceUserRolesAsync(user.Id, input.RoleIds);
        }

        await NotifyUserCreatedAsync(user);

        var dto = await MapToDtoAsync(user);
        await FillRolesAsync([dto]);
        return dto;
    }

    /// <summary>
    /// 更新指定用户；角色发生变化时吊销该用户的登录会话。
    /// </summary>
    [Permission(SystemPermissions.User.Update)]
    [SqlSugarUnitOfWork]
    public override async Task<UserDto> UpdateAsync(UpdateUserDto input)
    {
        await EnsureBuiltInAccountPreservedAsync(input);

        input.NickName = NormalizeRequiredNickName(input.NickName);
        input.Email = Normalize(input.Email);
        await EnsureUserIsUniqueAsync(input.UserName, input.Email, input.Id);
        await EnsureNickNameIsUniqueAsync(input.NickName, input.Id);

        if (input.RoleIds is not null)
        {
            await EnsureCanAssignRolesAsync();
            await EnsureAdminRoleRetainedAsync(input.Id, input.RoleIds);
        }

        var previousRoleIds = await GetUserRoleIdsAsync(input.Id);
        var dto = await base.UpdateAsync(input);

        if (input.RoleIds is not null)
        {
            await ReplaceUserRolesAsync(input.Id, input.RoleIds);

            var currentRoleIds = await GetUserRoleIdsAsync(input.Id);
            if (!previousRoleIds.OrderBy(id => id).SequenceEqual(currentRoleIds.OrderBy(id => id)))
            {
                await sessionRevoker.RevokeUsersAsync([input.Id]);
            }
        }

        await FillRolesAsync([dto]);
        return dto;
    }

    /// <summary>
    /// 将指定用户密码重置为默认密码并吊销其登录会话。
    /// </summary>
    [HttpPost]
    [Permission(SystemPermissions.User.ResetPassword)]
    [SqlSugarUnitOfWork]
    public async Task ResetUserPasswordAsync(Guid id)
    {
        var user = await userRepository.GetAsync(id)
            ?? throw new BusinessException(SystemErrorCodes.UserNotFound, "User not found.");
        if (user.IsBuiltIn)
        {
            throw CreateBuiltInAccountProtectedException(id);
        }

        var defaultPassword = await GetDefaultUserPasswordAsync();
        SetPassword(user, defaultPassword);
        ClearSecurityChallenge(user);
        await userRepository.UpdateAsync(user);
        await ClearE2eeAccountRecoveryAsync(id);
        await RevokeUserRefreshTokensAsync(id);
    }

    /// <summary>
    /// 删除指定用户并清理其关联数据。
    /// </summary>
    [Permission(SystemPermissions.User.Delete)]
    [SqlSugarUnitOfWork]
    public override async Task<bool> DeleteAsync(Guid id)
    {
        var user = await EnsureUserIsDeletableAsync(id);
        if (user == null)
        {
            return false;
        }

        return await DeleteUserCoreAsync(user);
    }

    /// <summary>
    /// 当前登录用户主动注销账号；需要当前密码确认，内置账号不可注销。
    /// </summary>
    [HttpPost]
    [SqlSugarUnitOfWork]
    [RateLimit(permitLimit: 3, windowSeconds: 60, PartitionName = "account-deletion")]
    public async Task PostDeleteMyAccountAsync(DeleteMyAccountRequest input)
    {
        var user = await GetCurrentUserEntityAsync();
        if (user.IsBuiltIn)
        {
            throw CreateBuiltInAccountProtectedException(user.Id);
        }

        if (!passwordManager.VerifyPassword(user, input.Password))
        {
            throw new BusinessException(
                SystemErrorCodes.CurrentPasswordInvalid,
                "The current password is invalid.");
        }

        await DeleteUserCoreAsync(user);
        DeleteRefreshTokenCookie();
    }

    private async Task<bool> DeleteUserCoreAsync(UserEntity user)
    {
        var id = user.Id;
        var links = await userRoleRepository.GetListAsync(link => link.UserId == id);
        if (links.Count > 0)
        {
            await userRoleRepository.DeleteManyAsync(links);
        }

        await userPreferenceRepository.DeleteAsync(preference => preference.UserId == id);
        // 删除身份行即 crypto-shredding：即使保留私有文章密文，也无法再解包 UMK/ACK。
        if (userE2eePasskeys is not null) await userE2eePasskeys.DeleteAsync(row => row.UserId == id);
        if (userE2eeChallenges is not null) await userE2eeChallenges.DeleteAsync(row => row.UserId == id);
        await userE2eeIdentityRepository.DeleteAsync(identity => identity.UserId == id);
        await sessionRevoker.RevokeUsersAsync([id]);

        // 各业务模块在用户行删除前清理或匿名化自己持有的用户数据，异常时整体回滚。
        var context = new UserDeletedContext(user.Id, user.TenantId);
        foreach (var handler in userDeletedHandlers ?? [])
        {
            await handler.HandleAsync(context);
        }

        return await base.DeleteAsync(id);
    }

    /// <summary>
    /// 获取 CSRF 防伪令牌。
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public Task<AntiforgeryTokenDto> GetAntiforgeryTokenAsync()
    {
        var tokens = antiforgery.GetAndStoreTokens(GetHttpContext());
        return Task.FromResult(new AntiforgeryTokenDto
        {
            RequestToken = tokens.RequestToken ?? string.Empty
        });
    }

    /// <summary>
    /// 自助注册账号；站点关闭自助注册时拒绝。
    /// </summary>
    [AllowAnonymous]
    [SqlSugarUnitOfWork]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "authentication")]
    public async Task<UserDto> RegisterAsync(CreateUserDto input)
    {
        if (!await IsSelfRegistrationEnabledAsync())
        {
            throw new BusinessException(
                SystemErrorCodes.SelfRegistrationDisabled,
                "Self-service registration is disabled on this site.");
        }

        input.NickName = NormalizeRequiredNickName(input.NickName);
        input.Email = Normalize(input.Email);
        await EnsureUserIsUniqueAsync(input.UserName, input.Email);
        await EnsureNickNameIsUniqueAsync(input.NickName);

        var password = input.Password ?? string.Empty;
        var (isValid, errors, minLength) = await passwordManager.ValidatePasswordAsync(password);
        if (!isValid)
        {
            throw CreatePasswordPolicyException(errors, minLength);
        }

        UserEntity user = await MapToEntityAsync(input);

        var encryptionInfo = passwordManager.EncryptPassword(password);
        user.EncryptedPassword = encryptionInfo.EncryptedPassword;
        user.Salt = encryptionInfo.Salt;

        await Repository.InsertAsync(user);
        await AssignRegistrationRolesAsync(user.Id);
        await NotifyUserCreatedAsync(user);
        return await MapToDtoAsync(user);
    }

    /// <summary>
    /// 用户名密码登录；启用两步验证时返回验证挑战而不直接签发会话。
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "authentication")]
    public async Task<LoginUserDto> LoginAsync(LoginRequest loginRequest)
    {
        var user = await AuthenticateLocalUserAsync(loginRequest.UserName, loginRequest.Password);

        var methods = GetEnabledTwoFactorMethods(user);
        if (methods.Count > 0)
        {
            var policy = await GetVerificationPolicyAsync();
            CreateSecurityChallenge(user, "login", policy.CodeExpirationMinutes);
            await userRepository.UpdateAsync(user);
            return new LoginUserDto
            {
                RequiresTwoFactor = true,
                TwoFactorChallengeId = user.SecurityChallengeId,
                TwoFactorMethods = methods
            };
        }

        return await IssueSessionAsync(
            user,
            loginRequest.DeviceId,
            loginRequest.DeviceType,
            loginRequest.DeviceName,
            loginRequest.RememberMe);
    }

    /// <summary>
    /// 校验用户名密码并维护失败计数与锁定；成功时清除失败状态并升级旧版密码哈希。
    /// </summary>
    private async Task<UserEntity> AuthenticateLocalUserAsync(string userName, string password)
    {
        // 参数验证
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessException(SystemErrorCodes.LoginCredentialsRequired, "User name and password are required.");
        }

        var user = await userRepository.GetAsync(u => u.UserName == userName);
        if (user == null)
        {
            // 避免用户枚举攻击，不要透露用户是否存在
            throw new BusinessException(SystemErrorCodes.InvalidCredentials, "Invalid user name or password.");
        }

        var utcNow = DateTime.UtcNow;
        if (!user.IsActive || user.LockoutEnd > utcNow)
        {
            throw new BusinessException(SystemErrorCodes.InvalidCredentials, "Invalid user name or password.");
        }

        if (user.TenantId is { } tenantId && !await tenantStatusValidator.IsActiveAsync(tenantId))
        {
            throw new BusinessException(SystemErrorCodes.InvalidCredentials, "Invalid user name or password.");
        }

        if (!passwordManager.VerifyPassword(user, password))
        {
            user.AccessFailedCount++;
            var (maxFailedAttempts, lockoutMinutes) = await GetLockoutPolicyAsync();
            if (user.AccessFailedCount >= maxFailedAttempts)
            {
                user.LockoutEnd = utcNow.AddMinutes(lockoutMinutes);
                user.AccessFailedCount = 0;
            }
            await userRepository.UpdateAsync(user);
            throw new BusinessException(SystemErrorCodes.InvalidCredentials, "Invalid user name or password.");
        }

        // 登录成功后清除失败状态，并将旧版 SM3 密码平滑升级为标准 PasswordHasher 格式。
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        if (!string.IsNullOrWhiteSpace(user.Salt))
        {
            var upgradedPassword = passwordManager.EncryptPassword(password);
            user.EncryptedPassword = upgradedPassword.EncryptedPassword;
            user.Salt = upgradedPassword.Salt;
        }
        await userRepository.UpdateAsync(user);
        return user;
    }

    /// <summary>
    /// 发送登录两步验证码。
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    [RateLimit(permitLimit: 5, windowSeconds: 60, PartitionName = "two-factor")]
    public async Task SendTwoFactorCodeAsync(TwoFactorCodeRequest input)
    {
        var method = NormalizeMessageMethod(input.Method);
        var user = await GetValidChallengeUserAsync(input.ChallengeId, "login");
        EnsureMethodEnabled(user, method);
        var policy = await GetVerificationPolicyAsync();
        var code = CreateVerificationCode(user, method, policy.CodeExpirationMinutes);
        await userRepository.UpdateAsync(user);

        if (!await TrySendCodeAsync(user, method, code, "login"))
        {
            ClearChallengeCode(user);
            await userRepository.UpdateAsync(user);
            throw new BusinessException(
                SystemErrorCodes.VerificationDeliveryUnavailable,
                "The verification message provider is unavailable.");
        }
    }

    /// <summary>
    /// 校验两步验证码并签发登录会话。
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "two-factor")]
    public async Task<LoginUserDto> CompleteTwoFactorLoginAsync(CompleteTwoFactorLoginRequest input)
    {
        var method = NormalizeTwoFactorMethod(input.Method);
        var user = await GetValidChallengeUserAsync(input.ChallengeId, "login");
        EnsureMethodEnabled(user, method);
        await VerifyChallengeCodeAsync(user, method, input.Code);
        await ConsumeSecurityChallengeAsync(user, input.ChallengeId);
        ClearSecurityChallenge(user);
        await userRepository.UpdateAsync(user);
        return await IssueSessionAsync(
            user,
            input.DeviceId,
            input.DeviceType,
            input.DeviceName,
            input.RememberMe);
    }

    /// <summary>
    /// 修改当前用户密码并吊销其登录会话。
    /// </summary>
    [HttpPost]
    [SqlSugarUnitOfWork]
    [RateLimit(permitLimit: 5, windowSeconds: 60, PartitionName = "password-change")]
    public async Task ChangePasswordAsync(ChangePasswordRequest input)
    {
        var user = await GetCurrentUserEntityAsync();
        if (!passwordManager.VerifyPassword(user, input.CurrentPassword))
        {
            throw new BusinessException(
                SystemErrorCodes.CurrentPasswordInvalid,
                "The current password is invalid.");
        }

        await ValidateNewPasswordAsync(input.NewPassword, input.ConfirmPassword);
        if (passwordManager.VerifyPassword(user, input.NewPassword))
        {
            throw new BusinessException(
                SystemErrorCodes.PasswordUnchanged,
                "The new password must be different from the current password.");
        }

        SetPassword(user, input.NewPassword);
        ClearSecurityChallenge(user);
        await userRepository.UpdateAsync(user);
        await ClearE2eeAccountRecoveryAsync(user.Id);

        await RevokeUserRefreshTokensAsync(user.Id);
        DeleteRefreshTokenCookie();
    }

    /// <summary>
    /// 申请密码重置验证码；对未知账号返回相同结构以避免账号枚举。
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    [RateLimit(permitLimit: 5, windowSeconds: 60, PartitionName = "password-reset")]
    public async Task<PasswordResetChallengeDto> RequestPasswordResetAsync(PasswordResetRequest input)
    {
        var method = NormalizeRecoveryMethod(input.Method);
        var challengeId = Guid.NewGuid();
        var policy = await GetVerificationPolicyAsync();
        var expiresInSeconds = policy.CodeExpirationMinutes * 60;
        var account = input.Account.Trim();
        var user = await userRepository.GetAsync(candidate =>
            candidate.UserName == account || candidate.Email == account || candidate.Phone == account);

        // 对未知账号返回同一结构，避免通过响应判断账号是否存在。
        if (user == null || !user.IsActive || !IsRecoveryMethodAvailable(user, method))
        {
            _ = HashVerificationCode(CreateNumericCode(), RandomNumberGenerator.GetBytes(16));
            return new PasswordResetChallengeDto
            {
                ChallengeId = challengeId,
                ExpiresInSeconds = expiresInSeconds
            };
        }

        CreateSecurityChallenge(user, "password-reset", policy.CodeExpirationMinutes, challengeId);
        if (method == TwoFactorMethods.Authenticator)
        {
            user.SecurityChallengeMethod = method;
            await userRepository.UpdateAsync(user);
        }
        else
        {
            var code = CreateVerificationCode(user, method, policy.CodeExpirationMinutes);
            await userRepository.UpdateAsync(user);
            try
            {
                if (!await TrySendCodeAsync(user, method, code, "password-reset"))
                {
                    ClearSecurityChallenge(user);
                    await userRepository.UpdateAsync(user);
                }
            }
            catch (Exception exception)
            {
                ClearSecurityChallenge(user);
                await userRepository.UpdateAsync(user);
                Logger.LogWarning(
                    "安全验证码发送失败，异常类型 {ExceptionType}；未记录目标地址或验证码",
                    exception.GetType().Name);
            }
        }

        return new PasswordResetChallengeDto
        {
            ChallengeId = challengeId,
            ExpiresInSeconds = expiresInSeconds
        };
    }

    /// <summary>
    /// 使用验证码重置密码并吊销该用户的登录会话。
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    [SqlSugarUnitOfWork]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "password-reset")]
    public async Task ResetPasswordAsync(ResetPasswordRequest input)
    {
        var user = await GetValidChallengeUserAsync(input.ChallengeId, "password-reset");
        var method = NormalizeRecoveryMethod(user.SecurityChallengeMethod);
        await VerifyChallengeCodeAsync(user, method, input.Code);
        await ValidateNewPasswordAsync(input.NewPassword, input.ConfirmPassword);
        await ConsumeSecurityChallengeAsync(user, input.ChallengeId);
        SetPassword(user, input.NewPassword);
        ClearSecurityChallenge(user);
        await userRepository.UpdateAsync(user);
        await ClearE2eeAccountRecoveryAsync(user.Id);
        await RevokeUserRefreshTokensAsync(user.Id);
    }

    /// <summary>
    /// 获取当前用户的两步验证状态。
    /// </summary>
    [HttpGet]
    public async Task<TwoFactorStatusDto> GetTwoFactorStatusAsync()
    {
        var user = await GetCurrentUserEntityAsync();
        return new TwoFactorStatusDto
        {
            AuthenticatorEnabled = user.AuthenticatorEnabled,
            EmailEnabled = user.EmailTwoFactorEnabled,
            SmsEnabled = user.SmsTwoFactorEnabled,
            MaskedEmail = MaskEmail(user.Email),
            MaskedPhone = MaskPhone(user.Phone),
            RecoveryCodesRemaining = ParseRecoveryCodeHashes(user).Count
        };
    }

    /// <summary>
    /// 开始配置身份验证器（TOTP），返回共享密钥与绑定 URI。
    /// </summary>
    [HttpPost]
    [RateLimit(permitLimit: 5, windowSeconds: 60, PartitionName = "two-factor-setup")]
    public async Task<AuthenticatorSetupDto> BeginAuthenticatorSetupAsync(
        BeginAuthenticatorSetupRequest input)
    {
        var user = await GetCurrentUserEntityAsync();
        EnsureCurrentPassword(user, input.CurrentPassword);
        var policy = await GetVerificationPolicyAsync();
        var secret = TotpUtility.GenerateSecret();
        user.PendingAuthenticatorKeyProtected = secretProtector.Protect(secret);
        user.PendingAuthenticatorExpiresAt = DateTime.UtcNow.AddMinutes(
            policy.AuthenticatorSetupMinutes);
        await userRepository.UpdateAsync(user);

        var sharedKey = TotpUtility.ToBase32(secret);
        var configuredIssuer = await systemConfigurationReader.GetValueAsync(
            SystemConfigurationKeys.AuthenticatorIssuer);
        var issuer = string.IsNullOrWhiteSpace(configuredIssuer)
            ? securityOptions.Value.AuthenticatorIssuer
            : configuredIssuer.Trim();
        var label = Uri.EscapeDataString($"{issuer}:{user.UserName}");
        var uri = $"otpauth://totp/{label}?secret={sharedKey}" +
                  $"&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
        return new AuthenticatorSetupDto
        {
            SharedKey = sharedKey,
            AuthenticatorUri = uri,
            ExpiresInSeconds = policy.AuthenticatorSetupMinutes * 60
        };
    }

    /// <summary>
    /// 确认身份验证器配置并生成恢复码。
    /// </summary>
    [HttpPost]
    [SqlSugarUnitOfWork]
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "two-factor-setup")]
    public async Task<AuthenticatorRecoveryCodesDto> ConfirmAuthenticatorSetupAsync(
        ConfirmAuthenticatorSetupRequest input)
    {
        var user = await GetCurrentUserEntityAsync();
        if (user.PendingAuthenticatorExpiresAt <= DateTime.UtcNow ||
            string.IsNullOrWhiteSpace(user.PendingAuthenticatorKeyProtected))
        {
            throw new BusinessException(
                SystemErrorCodes.AuthenticatorSetupExpired,
                "The authenticator setup has expired.");
        }

        var secret = secretProtector.Unprotect(user.PendingAuthenticatorKeyProtected);
        if (!TotpUtility.VerifyCode(secret, input.Code, DateTime.UtcNow))
        {
            throw new BusinessException(
                SystemErrorCodes.InvalidVerificationCode,
                "The verification code is invalid.");
        }

        var recoveryCodes = GenerateRecoveryCodes();
        user.AuthenticatorKeyProtected = user.PendingAuthenticatorKeyProtected;
        user.PendingAuthenticatorKeyProtected = null;
        user.PendingAuthenticatorExpiresAt = null;
        user.AuthenticatorEnabled = true;
        user.RecoveryCodeHashes = string.Join(';', recoveryCodes.Select(HashRecoveryCode));
        await userRepository.UpdateAsync(user);
        return new AuthenticatorRecoveryCodesDto { RecoveryCodes = recoveryCodes };
    }

    /// <summary>
    /// 申请短信或邮件两步验证配置的验证码。
    /// </summary>
    [HttpPost]
    [RateLimit(permitLimit: 5, windowSeconds: 60, PartitionName = "two-factor-setup")]
    public async Task<PasswordResetChallengeDto> RequestMessageTwoFactorSetupAsync(
        MessageTwoFactorSetupRequest input)
    {
        var method = NormalizeMessageMethod(input.Method);
        var user = await GetCurrentUserEntityAsync();
        EnsureCurrentPassword(user, input.CurrentPassword);
        EnsureDestinationAvailable(user, method);
        var policy = await GetVerificationPolicyAsync();
        CreateSecurityChallenge(user, "two-factor-setup", policy.CodeExpirationMinutes);
        var code = CreateVerificationCode(user, method, policy.CodeExpirationMinutes);
        await userRepository.UpdateAsync(user);
        if (!await TrySendCodeAsync(user, method, code, "two-factor-setup"))
        {
            ClearSecurityChallenge(user);
            await userRepository.UpdateAsync(user);
            throw new BusinessException(
                SystemErrorCodes.VerificationDeliveryUnavailable,
                "The verification message provider is unavailable.");
        }

        return new PasswordResetChallengeDto
        {
            ChallengeId = user.SecurityChallengeId!.Value,
            ExpiresInSeconds = policy.CodeExpirationMinutes * 60
        };
    }

    /// <summary>
    /// 确认短信或邮件两步验证配置。
    /// </summary>
    [HttpPost]
    public async Task ConfirmMessageTwoFactorSetupAsync(ConfirmMessageTwoFactorSetupRequest input)
    {
        var user = await GetValidChallengeUserAsync(input.ChallengeId, "two-factor-setup");
        var method = NormalizeMessageMethod(user.SecurityChallengeMethod);
        await VerifyChallengeCodeAsync(user, method, input.Code);
        await ConsumeSecurityChallengeAsync(user, input.ChallengeId);
        if (method == TwoFactorMethods.Email)
        {
            user.EmailTwoFactorEnabled = true;
        }
        else
        {
            user.SmsTwoFactorEnabled = true;
        }
        ClearSecurityChallenge(user);
        await userRepository.UpdateAsync(user);
    }

    /// <summary>
    /// 关闭指定的两步验证方式。
    /// </summary>
    [HttpPost]
    public async Task DisableTwoFactorAsync(DisableTwoFactorRequest input)
    {
        var method = NormalizeTwoFactorMethod(input.Method);
        var user = await GetCurrentUserEntityAsync();
        EnsureCurrentPassword(user, input.CurrentPassword);
        switch (method)
        {
            case TwoFactorMethods.Authenticator:
                user.AuthenticatorEnabled = false;
                user.AuthenticatorKeyProtected = null;
                user.RecoveryCodeHashes = null;
                break;
            case TwoFactorMethods.Email:
                user.EmailTwoFactorEnabled = false;
                break;
            case TwoFactorMethods.Sms:
                user.SmsTwoFactorEnabled = false;
                break;
            default:
                throw CreateUnavailableMethodException();
        }

        ClearSecurityChallenge(user);
        await userRepository.UpdateAsync(user);
    }

    private async Task<LoginUserDto> IssueSessionAsync(
        UserEntity user,
        string? deviceId,
        string? deviceType,
        string? deviceName,
        bool rememberMe = true)
    {
        if (!user.IsActive ||
            (user.TenantId is { } sessionTenantId &&
             !await tenantStatusValidator.IsActiveAsync(sessionTenantId)))
        {
            throw new BusinessException(SystemErrorCodes.InvalidCredentials, "Invalid user name or password.");
        }

        var utcNow = DateTime.UtcNow;
        var existingIdentifierHash = await GetCurrentDeviceIdentifierHashAsync(user.Id);
        var device = LoginDeviceClassifier.Classify(
            deviceId,
            deviceType,
            deviceName,
            GetUserAgent(),
            GetClientIpAddress(),
            existingIdentifierHash);
        await EnforceDeviceSessionLimitAsync(user.Id, device, utcNow);

        var userRoles = await userRoleRepository.GetListAsync(ur => ur.UserId == user.Id);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
        var roles = roleIds.Count == 0
            ? []
            : await roleRepository.GetListAsync(role => roleIds.Contains(role.Id));
        var permissions = await GetUserPermissionsAsync(roleIds);
        var sessionId = Guid.NewGuid();
        var claims = BuildUserClaims(user, roles, permissions, sessionId);
        var accessToken = passwordManager.GenerateAccessToken(claims);
        var refreshToken = CreateRefreshToken(rememberMe);

        await refreshTokenRepository.InsertAsync(new RefreshTokenEntity
        {
            UserId = user.Id,
            FamilyId = sessionId,
            Token = HashRefreshToken(refreshToken),
            ExpiresAt = utcNow.AddDays(securityOptions.Value.RefreshTokenExpirationDays),
            IsRevoked = false,
            CreationTime = utcNow,
            IpAddress = GetClientIpAddress(),
            UserAgent = GetUserAgent(),
            DeviceIdentifierHash = device.IdentifierHash,
            DeviceType = device.Type,
            DeviceName = device.Name,
            SessionStartedAt = utcNow,
            LastSeenAt = utcNow
        });

        WriteRefreshTokenCookie(refreshToken, rememberMe);
        return new LoginUserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Avatar = user.Avatar,
            Roles = roles.Select(role => new RoleInfo
            {
                Id = role.Id,
                Code = role.Code,
                Name = role.Name
            }).ToList(),
            Permissions = permissions.Select(permission => new PermissionInfo
            {
                Id = permission.Id,
                Code = permission.Code,
                Name = permission.Name
            }).ToList(),
            AccessToken = accessToken
        };
    }

    private async Task EnforceDeviceSessionLimitAsync(
        Guid userId,
        LoginDeviceDescriptor device,
        DateTime utcNow)
    {
        var limit = GetDeviceSessionLimit(device.Type);
        if (limit == 0)
        {
            throw new BusinessException(
                    SystemErrorCodes.DeviceTypeLoginDisabled,
                    "Login is disabled for this device type.")
                .WithData("DeviceType", device.Type);
        }

        var activeTokens = await refreshTokenRepository.GetActiveSessionTokensAsync(userId, utcNow);
        var sessions = activeTokens
            .GroupBy(token => token.FamilyId)
            .Select(group => group.OrderByDescending(token => token.CreationTime).First())
            .ToList();
        var sessionsToRevoke = LoginSessionLimitPolicy.SelectSessionsToRevoke(
            sessions.Select(token => new ActiveLoginSession(
                token.FamilyId,
                token.DeviceIdentifierHash,
                token.DeviceType,
                token.LastSeenAt ?? token.CreationTime)).ToList(),
            device,
            limit);

        // 批量吊销，减少 DB 往返次数
        if (sessionsToRevoke.Count > 0)
        {
            await refreshTokenRepository.RevokeFamiliesAsync(userId, sessionsToRevoke, utcNow);
        }
    }

    private int GetDeviceSessionLimit(string deviceType) => deviceType switch
    {
        LoginDeviceTypes.Pad => securityOptions.Value.DeviceSessionLimits.Pad,
        LoginDeviceTypes.Phone => securityOptions.Value.DeviceSessionLimits.Phone,
        _ => securityOptions.Value.DeviceSessionLimits.Pc
    };

    private async Task<string?> GetCurrentDeviceIdentifierHashAsync(Guid userId)
    {
        var refreshToken = ReadRefreshTokenCookie();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var token = await refreshTokenRepository.GetQueryable()
            .FirstAsync(candidate =>
                candidate.UserId == userId &&
                candidate.Token == tokenHash &&
                !candidate.IsRevoked);
        return token?.DeviceIdentifierHash;
    }

    private async Task<UserEntity> GetCurrentUserEntityAsync()
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        return await userRepository.GetAsync(userId)
            ?? throw new BusinessException(SystemErrorCodes.UserNotFound, "User not found.");
    }

    private async Task<UserEntity> GetValidChallengeUserAsync(Guid challengeId, string purpose)
    {
        var user = await userRepository.GetAsync(candidate =>
            candidate.SecurityChallengeId == challengeId);
        if (user == null ||
            !user.IsActive ||
            user.SecurityChallengeExpiresAt <= DateTime.UtcNow ||
            !string.Equals(user.SecurityChallengePurpose, purpose, StringComparison.Ordinal))
        {
            throw new BusinessException(
                SystemErrorCodes.InvalidSecurityChallenge,
                "The security challenge is invalid or expired.");
        }

        return user;
    }

    private async Task ConsumeSecurityChallengeAsync(UserEntity user, Guid challengeId)
    {
        if (!await userRepository.TryConsumeSecurityChallengeAsync(
                user.Id,
                challengeId,
                DateTime.UtcNow))
        {
            throw new BusinessException(
                SystemErrorCodes.InvalidSecurityChallenge,
                "The security challenge has already been used or expired.");
        }
    }

    private void CreateSecurityChallenge(
        UserEntity user,
        string purpose,
        int expiresMinutes,
        Guid? challengeId = null)
    {
        user.SecurityChallengeId = challengeId ?? Guid.NewGuid();
        user.SecurityChallengePurpose = purpose;
        user.SecurityChallengeMethod = null;
        user.SecurityChallengeCodeHash = null;
        user.SecurityChallengeSalt = null;
        user.SecurityChallengeAttempts = 0;
        user.SecurityChallengeExpiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);
    }

    private static void ClearSecurityChallenge(UserEntity user)
    {
        user.SecurityChallengeId = null;
        user.SecurityChallengePurpose = null;
        user.SecurityChallengeMethod = null;
        user.SecurityChallengeCodeHash = null;
        user.SecurityChallengeSalt = null;
        user.SecurityChallengeExpiresAt = null;
        user.SecurityChallengeAttempts = 0;
    }

    private static void ClearChallengeCode(UserEntity user)
    {
        user.SecurityChallengeMethod = null;
        user.SecurityChallengeCodeHash = null;
        user.SecurityChallengeSalt = null;
        user.SecurityChallengeAttempts = 0;
    }

    private string CreateVerificationCode(UserEntity user, string method, int expiresMinutes)
    {
        var code = CreateNumericCode();
        var salt = RandomNumberGenerator.GetBytes(16);
        user.SecurityChallengeMethod = method;
        user.SecurityChallengeSalt = Convert.ToBase64String(salt);
        user.SecurityChallengeCodeHash = HashVerificationCode(code, salt);
        user.SecurityChallengeAttempts = 0;
        user.SecurityChallengeExpiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);
        return code;
    }

    private async Task VerifyChallengeCodeAsync(UserEntity user, string method, string code)
    {
        var valid = method switch
        {
            TwoFactorMethods.Authenticator => VerifyAuthenticatorCode(user, code),
            TwoFactorMethods.RecoveryCode => ConsumeRecoveryCode(user, code),
            TwoFactorMethods.Email or TwoFactorMethods.Sms =>
                VerifyStoredCode(user, method, code),
            _ => false
        };
        if (valid)
        {
            return;
        }

        var policy = await GetVerificationPolicyAsync();
        user.SecurityChallengeAttempts++;
        if (user.SecurityChallengeAttempts >= policy.CodeMaxAttempts)
        {
            ClearSecurityChallenge(user);
        }
        await userRepository.UpdateAsync(user);
        throw new BusinessException(
            SystemErrorCodes.InvalidVerificationCode,
            "The verification code is invalid.");
    }

    private bool VerifyStoredCode(UserEntity user, string method, string code)
    {
        if (!string.Equals(user.SecurityChallengeMethod, method, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(user.SecurityChallengeCodeHash) ||
            string.IsNullOrWhiteSpace(user.SecurityChallengeSalt))
        {
            return false;
        }

        var salt = Convert.FromBase64String(user.SecurityChallengeSalt);
        var actual = Convert.FromBase64String(HashVerificationCode(code.Trim(), salt));
        var expected = Convert.FromBase64String(user.SecurityChallengeCodeHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private bool VerifyAuthenticatorCode(UserEntity user, string code)
    {
        if (!user.AuthenticatorEnabled || string.IsNullOrWhiteSpace(user.AuthenticatorKeyProtected))
        {
            return false;
        }

        try
        {
            return TotpUtility.VerifyCode(
                secretProtector.Unprotect(user.AuthenticatorKeyProtected),
                code,
                DateTime.UtcNow);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool ConsumeRecoveryCode(UserEntity user, string code)
    {
        var hashes = ParseRecoveryCodeHashes(user);
        var suppliedHash = HashRecoveryCode(code);
        var match = hashes.FirstOrDefault(hash =>
            CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(hash),
                Convert.FromHexString(suppliedHash)));
        if (match == null)
        {
            return false;
        }

        hashes.Remove(match);
        user.RecoveryCodeHashes = hashes.Count == 0 ? null : string.Join(';', hashes);
        return true;
    }

    private static List<string> ParseRecoveryCodeHashes(UserEntity user) =>
        string.IsNullOrWhiteSpace(user.RecoveryCodeHashes)
            ? []
            : user.RecoveryCodeHashes.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static IReadOnlyList<string> GenerateRecoveryCodes() =>
        Enumerable.Range(0, 10)
            .Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(5)))
            .Select(value => $"{value[..5]}-{value[5..]}")
            .ToArray();

    private static string HashRecoveryCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            code.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant())));

    private static string CreateNumericCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string HashVerificationCode(string code, byte[] salt) =>
        Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(
            code,
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32));

    private async Task<bool> TrySendCodeAsync(
        UserEntity user,
        string method,
        string code,
        string purpose)
    {
        var destination = method == TwoFactorMethods.Email ? user.Email : user.Phone;
        return !string.IsNullOrWhiteSpace(destination) &&
               await verificationMessageSender.SendAsync(method, destination, code, purpose);
    }

    private static string NormalizeTwoFactorMethod(string? method)
    {
        var normalized = method?.Trim().ToLowerInvariant();
        return normalized is TwoFactorMethods.Authenticator or
            TwoFactorMethods.Email or
            TwoFactorMethods.Sms or
            TwoFactorMethods.RecoveryCode
            ? normalized
            : throw CreateUnavailableMethodException();
    }

    private static string NormalizeMessageMethod(string? method)
    {
        var normalized = NormalizeTwoFactorMethod(method);
        return normalized is TwoFactorMethods.Email or TwoFactorMethods.Sms
            ? normalized
            : throw CreateUnavailableMethodException();
    }

    private static string NormalizeRecoveryMethod(string? method)
    {
        var normalized = NormalizeTwoFactorMethod(method);
        return normalized is TwoFactorMethods.Email or
            TwoFactorMethods.Sms or
            TwoFactorMethods.Authenticator
            ? normalized
            : throw CreateUnavailableMethodException();
    }

    private static BusinessException CreateUnavailableMethodException() =>
        new(
            SystemErrorCodes.TwoFactorMethodUnavailable,
            "The selected verification method is unavailable.");

    private static void EnsureDestinationAvailable(UserEntity user, string method)
    {
        var destination = method == TwoFactorMethods.Email ? user.Email : user.Phone;
        if (string.IsNullOrWhiteSpace(destination))
        {
            throw CreateUnavailableMethodException();
        }
    }

    private static void EnsureMethodEnabled(UserEntity user, string method)
    {
        var available = method switch
        {
            TwoFactorMethods.Authenticator => user.AuthenticatorEnabled,
            TwoFactorMethods.Email => user.EmailTwoFactorEnabled,
            TwoFactorMethods.Sms => user.SmsTwoFactorEnabled,
            TwoFactorMethods.RecoveryCode => ParseRecoveryCodeHashes(user).Count > 0,
            _ => false
        };
        if (!available)
        {
            throw CreateUnavailableMethodException();
        }
    }

    private static bool IsRecoveryMethodAvailable(UserEntity user, string method) =>
        method switch
        {
            TwoFactorMethods.Authenticator => user.AuthenticatorEnabled,
            TwoFactorMethods.Email => !string.IsNullOrWhiteSpace(user.Email),
            TwoFactorMethods.Sms => !string.IsNullOrWhiteSpace(user.Phone),
            _ => false
        };

    private static IReadOnlyList<string> GetEnabledTwoFactorMethods(UserEntity user)
    {
        var methods = new List<string>();
        if (user.AuthenticatorEnabled) methods.Add(TwoFactorMethods.Authenticator);
        if (user.EmailTwoFactorEnabled) methods.Add(TwoFactorMethods.Email);
        if (user.SmsTwoFactorEnabled) methods.Add(TwoFactorMethods.Sms);
        if (ParseRecoveryCodeHashes(user).Count > 0) methods.Add(TwoFactorMethods.RecoveryCode);
        return methods;
    }

    private void EnsureCurrentPassword(UserEntity user, string currentPassword)
    {
        if (!passwordManager.VerifyPassword(user, currentPassword))
        {
            throw new BusinessException(
                SystemErrorCodes.CurrentPasswordInvalid,
                "The current password is invalid.");
        }
    }

    private async Task ValidateNewPasswordAsync(string password, string confirmation)
    {
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            throw new BusinessException(
                SystemErrorCodes.PasswordConfirmationMismatch,
                "The password confirmation does not match.");
        }

        var (isValid, errors, minLength) = await passwordManager.ValidatePasswordAsync(password);
        if (!isValid)
        {
            throw CreatePasswordPolicyException(errors, minLength);
        }
    }

    private void SetPassword(UserEntity user, string password)
    {
        var encryptionInfo = passwordManager.EncryptPassword(password);
        user.EncryptedPassword = encryptionInfo.EncryptedPassword;
        user.Salt = encryptionInfo.Salt;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
    }

    /// <summary>
    /// 邮箱找回或管理员重置登录密码后无法重新包裹，账号密码备份立即失效；
    /// 清除密文避免留下不可用的恢复材料。
    /// </summary>
    internal async Task ClearE2eeAccountRecoveryAsync(Guid userId)
    {
        var identity = await userE2eeIdentityRepository.GetAsync(
            item => item.UserId == userId && item.IsActive);
        if (identity is null ||
            (identity.AccountWrappedRecoveryKey is null &&
             identity.AccountKdfSalt is null &&
             identity.AccountKdfIterations is null))
        {
            return;
        }

        identity.AccountWrappedRecoveryKey = null;
        identity.AccountKdfSalt = null;
        identity.AccountKdfIterations = null;
        identity.AccountRecoveryKeyUpdatedAt = null;
        identity.Revision += 1;
        await userE2eeIdentityRepository.UpdateAsync(identity);
        Logger.LogInformation(
            "E2EE account recovery backup cleared after login password change. UserId={UserId}",
            userId);
    }

    private async Task RevokeUserRefreshTokensAsync(Guid userId)
    {
        await sessionRevoker.RevokeUsersAsync([userId]);
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var separator = email.IndexOf('@');
        if (separator <= 0) return "***";
        var name = email[..separator];
        return $"{name[0]}***{email[separator..]}";
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        return phone.Length <= 4 ? "****" : $"***{phone[^4..]}";
    }

    private async Task<List<PermissionEntity>> GetUserPermissionsAsync(List<Guid> roleIds)
    {
        var permissions = new List<PermissionEntity>();

        if (roleIds.Any())
        {
            // 获取角色权限关联
            var rolePermissions = await rolePermissionRepository.GetListAsync(rp => roleIds.Contains(rp.RoleId));
            var permissionIds = rolePermissions.Select(rp => rp.PermissionId).Distinct().ToList();

            // 批量获取权限详情，避免循环查询
            if (permissionIds.Any())
            {
                permissions = await permissionRepository.GetListAsync(p => permissionIds.Contains(p.Id));
            }
        }

        return permissions;
    }

    /// <summary>
    /// 刷新 Access Token
    /// </summary>
    /// <returns>新的访问令牌和刷新令牌</returns>
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RateLimit(permitLimit: 30, windowSeconds: 60, PartitionName = "token-refresh")]
    public async Task<LoginUserDto> RefreshTokenAsync()
    {
        var refreshToken = ReadRefreshTokenCookie();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            DeleteRefreshTokenCookie();
            throw new AuthorizationException(SystemErrorCodes.RefreshTokenCookieMissing, "The refresh token cookie is missing.");
        }

        var refreshTokenHash = HashRefreshToken(refreshToken);
        var tokenEntity = await refreshTokenRepository.GetQueryable().FirstAsync(t => t.Token == refreshTokenHash);
            
        if (tokenEntity == null)
        {
            DeleteRefreshTokenCookie();
            throw new BusinessException(SystemErrorCodes.InvalidRefreshToken, "The refresh token is invalid.");
        }

        if (tokenEntity.IsRevoked)
        {
            await sessionRevoker.RevokeSessionAsync(tokenEntity.UserId, tokenEntity.FamilyId);
            DeleteRefreshTokenCookie();
            throw new BusinessException(SystemErrorCodes.RefreshTokenReuseDetected, "Refresh token reuse was detected; the token family has been revoked.");
        }

        if (!tokenEntity.IsActive)
        {
            DeleteRefreshTokenCookie();
            throw new BusinessException(SystemErrorCodes.RefreshTokenExpired, "The refresh token has expired.");
        }

        // 获取用户信息
        var user = await userRepository.GetAsync(tokenEntity.UserId);
        if (user == null)
        {
            DeleteRefreshTokenCookie();
            throw new BusinessException(SystemErrorCodes.UserNotFound, "The user no longer exists.");
        }

        if (!user.IsActive ||
            (user.TenantId is { } refreshTenantId &&
             !await tenantStatusValidator.IsActiveAsync(refreshTenantId)))
        {
            await sessionRevoker.RevokeUsersAsync([user.Id]);
            DeleteRefreshTokenCookie();
            throw new BusinessException(
                SystemErrorCodes.InvalidRefreshToken,
                "The login session is no longer active.");
        }

        // 获取用户角色和权限
        var userRoles = await userRoleRepository.GetListAsync(ur => ur.UserId == user.Id);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
        
        // 获取角色详情
        var roles = new List<RoleEntity>();
        if (roleIds.Any())
        {
            roles = await roleRepository.GetListAsync(r => roleIds.Contains(r.Id));
        }
        
        var permissions = await GetUserPermissionsAsync(roleIds);

        var utcNow = DateTime.UtcNow;
        var fallbackDevice = LoginDeviceClassifier.Classify(
            null,
            tokenEntity.DeviceType,
            tokenEntity.DeviceName,
            GetUserAgent(),
            GetClientIpAddress(),
            tokenEntity.DeviceIdentifierHash);

        // 构建Claims
        var claims = BuildUserClaims(user, roles, permissions, tokenEntity.FamilyId);

        // 生成新的 Access Token
        var newAccessToken = passwordManager.GenerateAccessToken(claims);

        // 轮换间隔内的刷新不更换 Refresh Token：页面导航会中止在途请求（499），
        // 若此时已完成轮换，客户端仍持有旧 Cookie，下一次刷新将触发复用检测并吊销整个会话族。
        // 0 表示每次刷新都轮换（兼容旧行为）。
        var rotationMinutes = Math.Max(0, securityOptions.Value.RefreshTokenRotationMinutes);
        var rotationDue = tokenEntity.CreationTime <= utcNow.AddMinutes(-rotationMinutes);
        var persistentCookie = IsPersistentRefreshToken(refreshToken);
        if (rotationDue)
        {
            var newRefreshToken = CreateRefreshToken(persistentCookie);
            var newRefreshTokenHash = HashRefreshToken(newRefreshToken);

            // 条件更新保证同一个 refresh token 只能轮换一次；并发重复使用会撤销整个令牌家族。
            var rotated = await refreshTokenRepository.TryRotateAsync(
                tokenEntity.Id,
                newRefreshTokenHash,
                utcNow);
            if (!rotated)
            {
                await sessionRevoker.RevokeSessionAsync(tokenEntity.UserId, tokenEntity.FamilyId);
                DeleteRefreshTokenCookie();
                throw new BusinessException(SystemErrorCodes.RefreshTokenAlreadyUsed, "The refresh token has already been used.");
            }

            // 保存新的 refresh token
            var newTokenEntity = new RefreshTokenEntity
            {
                UserId = user.Id,
                FamilyId = tokenEntity.FamilyId,
                Token = newRefreshTokenHash,
                ExpiresAt = utcNow.AddDays(securityOptions.Value.RefreshTokenExpirationDays),
                IsRevoked = false,
                CreationTime = utcNow,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                DeviceIdentifierHash = fallbackDevice.IdentifierHash,
                DeviceType = fallbackDevice.Type,
                DeviceName = fallbackDevice.Name,
                SessionStartedAt = tokenEntity.SessionStartedAt ?? tokenEntity.CreationTime,
                LastSeenAt = utcNow
            };
            await refreshTokenRepository.InsertAsync(newTokenEntity);
            WriteRefreshTokenCookie(newRefreshToken, persistentCookie);
        }
        else
        {
            await refreshTokenRepository.TouchSessionAsync(
                tokenEntity.UserId,
                tokenEntity.FamilyId,
                utcNow,
                utcNow.AddMinutes(-1));
        }

        var result = new LoginUserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Avatar = user.Avatar,
            Roles = roles.Select(r => new RoleInfo
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name
            }).ToList(),
            Permissions = permissions.Select(p => new PermissionInfo
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name
            }).ToList(),
            AccessToken = newAccessToken
        };
        return result;
    }

    /// <summary>
    /// 撤销 Refresh Token（用于登出）
    /// 需要有效 JWT 认证 + CSRF 令牌双重保护。
    /// JWT 过期时服务端无法完成撤销，前端仍会清理本地会话。
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RateLimit(permitLimit: 30, windowSeconds: 60, PartitionName = "token-revoke")]
    public async Task RevokeRefreshTokenAsync()
    {
        var refreshToken = ReadRefreshTokenCookie();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            DeleteRefreshTokenCookie();
            return;
        }

        try
        {
            var refreshTokenHash = HashRefreshToken(refreshToken);
            var tokenEntity = await refreshTokenRepository.GetQueryable()
                .FirstAsync(t => t.Token == refreshTokenHash);

            if (tokenEntity == null || tokenEntity.IsRevoked)
            {
                return;
            }

            await sessionRevoker.RevokeSessionAsync(tokenEntity.UserId, tokenEntity.FamilyId);
        }
        finally
        {
            DeleteRefreshTokenCookie();
        }
    }
    
    /// <summary>
    /// 撤销用户的所有 Refresh Token（用于强制登出所有设备）
    /// </summary>
    /// <param name="userId">用户ID</param>
    [HttpPost]
    [Permission(SystemPermissions.Token.RevokeAll)]
    public async Task RevokeAllRefreshTokensAsync(Guid userId)
    {
        await sessionRevoker.RevokeUsersAsync([userId]);
    }

    /// <summary>
    /// 获取当前用户的登录会话列表。
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<LoginSessionDto>> GetMyLoginSessionsAsync()
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var utcNow = DateTime.UtcNow;
        var currentSessionId = GetCurrentSessionId();
        var tokens = await refreshTokenRepository.GetActiveSessionTokensAsync(userId, utcNow);
        var onlineThreshold = utcNow.AddMinutes(-securityOptions.Value.SessionTimeoutMinutes);

        return tokens
            .GroupBy(token => token.FamilyId)
            .Select(group => group.OrderByDescending(token => token.CreationTime).First())
            .OrderByDescending(token => token.LastSeenAt ?? token.CreationTime)
            .Select(token => new LoginSessionDto
            {
                Id = token.FamilyId,
                DeviceType = token.DeviceType ?? LoginDeviceTypes.Pc,
                DeviceName = token.DeviceName ?? token.DeviceType ?? LoginDeviceTypes.Pc,
                IpAddress = token.IpAddress,
                LoginAt = token.SessionStartedAt ?? token.CreationTime,
                LastSeenAt = token.LastSeenAt ?? token.CreationTime,
                ExpiresAt = token.ExpiresAt,
                IsCurrent = token.FamilyId == currentSessionId,
                IsOnline = (token.LastSeenAt ?? token.CreationTime) >= onlineThreshold
            })
            .ToList();
    }

    /// <summary>
    /// 吊销当前用户的指定登录会话。
    /// </summary>
    [HttpPost]
    public async Task RevokeMyLoginSessionAsync(Guid sessionId)
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var affectedRows = await sessionRevoker.RevokeSessionAsync(userId, sessionId);
        if (affectedRows == 0)
        {
            throw new BusinessException(
                SystemErrorCodes.LoginSessionNotFound,
                "The login session was not found.");
        }

        if (sessionId == GetCurrentSessionId())
        {
            DeleteRefreshTokenCookie();
        }
    }
    
    /// <summary>
    /// 清理过期的 Refresh Token（管理员功能）
    /// </summary>
    [HttpPost]
    [SqlSugarUnitOfWork]
    [Permission(SystemPermissions.Token.Cleanup)]
    public async Task<int> CleanupExpiredRefreshTokensAsync()
    {
        // 获取所有过期的 token
        var expiredTokens = await refreshTokenRepository.GetListAsync(
            t => t.ExpiresAt < DateTime.UtcNow
        );

        if (expiredTokens.Count == 0)
        {
            return 0;
        }

        // 删除过期的 token
        foreach (var token in expiredTokens)
        {
            await refreshTokenRepository.DeleteAsync(token.Id);
        }

        return expiredTokens.Count;
    }

    /// <summary>
    /// 构建用户的 JWT Claims
    /// </summary>
    private List<Claim> BuildUserClaims(
        UserEntity user,
        List<RoleEntity> roles,
        List<PermissionEntity> permissions,
        Guid sessionId)
    {
        var claims = new List<Claim>
        {
            new (TokenTypeConst.Id, user.Id.ToString()),
            new (TokenTypeConst.UserName, user.UserName),
            new (ClaimTypes.NameIdentifier, user.Id.ToString()), // 标准 claim，用于兼容
            new (ClaimTypes.Name, user.UserName), // 标准 claim，用于兼容
            new (TokenTypeConst.SessionId, sessionId.ToString()),
            new (TokenTypeConst.JwtId, Guid.NewGuid().ToString()), // JWT ID
            new (TokenTypeConst.JwtIssuerAt, new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64) // Issued at
        };

        // 添加用户名字
        if (!string.IsNullOrEmpty(user.Name))
        {
            claims.Add(new Claim(TokenTypeConst.Name, user.Name));
        }

        // 添加用户姓氏
        if (!string.IsNullOrEmpty(user.SurName))
        {
            claims.Add(new Claim(TokenTypeConst.SurName, user.SurName));
        }

        // 添加邮箱
        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(TokenTypeConst.Email, user.Email));
        }

        // 添加电话
        if (!string.IsNullOrEmpty(user.Phone))
        {
            claims.Add(new Claim(TokenTypeConst.Phone, user.Phone));
        }

        // 多租户：写入 tenant_id claim（Host 租户不写，tenant_id=null 即 Host）
        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim(TokenTypeConst.TenantId, user.TenantId.Value.ToString()));
        }

        // 添加角色信息
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Code)); // 标准 claim
        }

        // 内置角色标记只对宿主身份生效：租户管理员不得凭此获得全局权限绕过。
        if (user.TenantId is null && roles.Any(role => role.IsBuiltIn))
        {
            claims.Add(new Claim(TokenTypeConst.IsBuiltInRole, bool.TrueString));
        }

        // 添加权限信息
        foreach (var permission in permissions)
        {
            claims.Add(new Claim(TokenTypeConst.Permission, permission.Code));
        }

        return claims;
    }

    private HttpContext GetHttpContext() =>
        httpContextAccessor.HttpContext ??
        throw new InvalidOperationException("浏览器会话接口只能在 HTTP 请求中调用");

    private Guid? GetCurrentSessionId()
    {
        var value = GetHttpContext().User.FindFirstValue(TokenTypeConst.SessionId);
        return Guid.TryParse(value, out var sessionId) ? sessionId : null;
    }

    private string? ReadRefreshTokenCookie() =>
        GetHttpContext().Request.Cookies[securityOptions.Value.RefreshTokenCookieName];

    // Refresh Token 值自身携带 Cookie 作用域：p: 为持久 Cookie，s: 为关闭浏览器即失效的会话 Cookie。
    // 前缀参与哈希，被篡改后无法命中数据库记录；历史令牌无前缀，按持久会话处理。
    private const string PersistentRefreshTokenPrefix = "p:";
    private const string SessionScopedRefreshTokenPrefix = "s:";

    private string CreateRefreshToken(bool persistent) =>
        (persistent ? PersistentRefreshTokenPrefix : SessionScopedRefreshTokenPrefix) +
        passwordManager.GenerateRefreshToken();

    private static bool IsPersistentRefreshToken(string refreshToken) =>
        !refreshToken.StartsWith(SessionScopedRefreshTokenPrefix, StringComparison.Ordinal);

    private void WriteRefreshTokenCookie(string refreshToken, bool persistent)
    {
        GetHttpContext().Response.Cookies.Append(
            securityOptions.Value.RefreshTokenCookieName,
            refreshToken,
            CreateRefreshTokenCookieOptions(
                persistent
                    ? DateTimeOffset.UtcNow.AddDays(securityOptions.Value.RefreshTokenExpirationDays)
                    : null));
    }

    private void DeleteRefreshTokenCookie()
    {
        GetHttpContext().Response.Cookies.Delete(
            securityOptions.Value.RefreshTokenCookieName,
            CreateRefreshTokenCookieOptions(DateTimeOffset.UnixEpoch));
    }

    private CookieOptions CreateRefreshTokenCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = securityOptions.Value.RefreshTokenCookieSecure,
        SameSite = SameSiteMode.Strict,
        IsEssential = true,
        Path = "/api/system/user",
        Expires = expires
    };

    /// <summary>
    /// 获取客户端 IP 地址
    /// </summary>
    private string? GetClientIpAddress()
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            // 代理头只由 ForwardedHeadersMiddleware 在可信代理配置下解析。
            return httpContext.Connection.RemoteIpAddress?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取用户代理信息
    /// </summary>
    private string? GetUserAgent()
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var userAgent = httpContext.Request.Headers["User-Agent"].FirstOrDefault();
            
            // 限制长度，避免存储过长的字符串
            if (!string.IsNullOrEmpty(userAgent) && userAgent.Length > 500)
            {
                return userAgent.Substring(0, 500);
            }

            return userAgent;
        }
        catch
        {
            return null;
        }
    }

    private BusinessException CreatePasswordPolicyException(
        IReadOnlyList<string> errors,
        int minLength)
    {
        var code = errors.FirstOrDefault() ?? SystemErrorCodes.PasswordRequired;
        return new BusinessException(code, "The password does not satisfy the password policy.")
            .WithData("MinLength", minLength);
    }

    private async Task<bool> IsSelfRegistrationEnabledAsync()
    {
        var value = await systemConfigurationReader.GetValueAsync(
            SystemConfigurationKeys.RegistrationEnabled);
        return bool.TryParse(value, out var enabled) && enabled;
    }

    /// <summary>
    /// 为自助注册用户分配系统配置的角色。配置缺失时回退到内置普通用户角色；
    /// 内置角色（如 Admin）不允许通过注册配置下发，避免匿名注册获得管理权限。
    /// </summary>
    private async Task AssignRegistrationRolesAsync(Guid userId)
    {
        var configured = await systemConfigurationReader.GetValueAsync(
            SystemConfigurationKeys.RegistrationRoleCodes);
        var roleCodes = ParseRoleCodes(configured);
        if (roleCodes.Count == 0)
        {
            roleCodes = [RoleConst.User];
        }

        var roles = (await roleRepository.GetAll())
            .Where(role => !role.IsBuiltIn &&
                roleCodes.Any(code => string.Equals(code, role.Code, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (roles.Count == 0)
        {
            logger.LogWarning(
                "自助注册未匹配到可分配角色，注册角色配置为 {RoleCodes}",
                configured ?? "(default: User)");
            return;
        }

        var assignedRoleIds = (await userRoleRepository.GetListAsync(link => link.UserId == userId))
            .Select(link => link.RoleId)
            .ToHashSet();
        foreach (var role in roles)
        {
            if (!assignedRoleIds.Add(role.Id))
            {
                continue;
            }

            await userRoleRepository.InsertAsync(new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            });
        }
    }

    private static List<string> ParseRoleCodes(string? configured) =>
        (configured ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<string> GetDefaultUserPasswordAsync()
    {
        var configured = await systemConfigurationReader.GetValueAsync(
            SystemConfigurationKeys.UserDefaultPassword);
        var password = string.IsNullOrWhiteSpace(configured)
            ? configuration["App:AdminDefaultPassword"]
            : configured;

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessException(
                SystemErrorCodes.DefaultUserPasswordNotConfigured,
                "The default user password is not configured.");
        }

        password = password.Trim();
        var (isValid, _, _) = await passwordManager.ValidatePasswordAsync(password);
        if (!isValid)
        {
            throw new BusinessException(
                SystemErrorCodes.DefaultUserPasswordInvalid,
                "The configured default user password does not satisfy the password policy.");
        }

        return password;
    }

    private async Task<VerificationPolicy> GetVerificationPolicyAsync()
    {
        var values = await systemConfigurationReader.GetValuesAsync(
            SystemConfigurationKeys.VerificationCodeExpirationMinutes,
            SystemConfigurationKeys.VerificationCodeMaxAttempts,
            SystemConfigurationKeys.AuthenticatorSetupExpirationMinutes);
        var options = securityOptions.Value;
        return new VerificationPolicy(
            ReadBoundedInt(
                values.GetValueOrDefault(SystemConfigurationKeys.VerificationCodeExpirationMinutes),
                1, 15, options.VerificationCodeExpirationMinutes),
            ReadBoundedInt(
                values.GetValueOrDefault(SystemConfigurationKeys.VerificationCodeMaxAttempts),
                3, 10, options.VerificationCodeMaxAttempts),
            ReadBoundedInt(
                values.GetValueOrDefault(SystemConfigurationKeys.AuthenticatorSetupExpirationMinutes),
                1, 1440, options.AuthenticatorSetupExpirationMinutes));
    }

    private static int ReadBoundedInt(string? value, int min, int max, int fallback) =>
        int.TryParse(value, out var parsed) && parsed >= min && parsed <= max ? parsed : fallback;

    private sealed record VerificationPolicy(
        int CodeExpirationMinutes,
        int CodeMaxAttempts,
        int AuthenticatorSetupMinutes);

    private async Task<(int MaxFailedAttempts, int LockoutMinutes)> GetLockoutPolicyAsync()
    {
        var values = await systemConfigurationReader.GetValuesAsync(
            SystemConfigurationKeys.LoginMaxFailedAttempts,
            SystemConfigurationKeys.LockoutDurationMinutes);

        var maxFailedAttempts = int.TryParse(
            values.GetValueOrDefault(SystemConfigurationKeys.LoginMaxFailedAttempts),
            out var parsedAttempts) && parsedAttempts > 0
            ? parsedAttempts
            : securityOptions.Value.LoginFailureLockoutThreshold;

        var lockoutMinutes = int.TryParse(
            values.GetValueOrDefault(SystemConfigurationKeys.LockoutDurationMinutes),
            out var parsedMinutes) && parsedMinutes > 0
            ? parsedMinutes
            : securityOptions.Value.LockoutDurationMinutes;

        return (maxFailedAttempts, lockoutMinutes);
    }

    private async Task NotifyUserCreatedAsync(UserEntity user)
    {
        if (userCreatedHandlers == null)
        {
            return;
        }

        var context = new UserCreatedContext(user.Id, user.UserName, user.NickName, user.TenantId);
        foreach (var handler in userCreatedHandlers)
        {
            await handler.HandleAsync(context);
        }
    }

    private async Task<List<Guid>> GetUserRoleIdsAsync(Guid userId)
    {
        var links = await userRoleRepository.GetListAsync(link => link.UserId == userId);
        return links.Select(link => link.RoleId).ToList();
    }

    private async Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds)
    {
        var distinctRoleIds = roleIds.Distinct().ToList();
        if (distinctRoleIds.Count > 0)
        {
            var existingRoles = await roleRepository.GetListAsync(
                role => distinctRoleIds.Contains(role.Id));
            if (existingRoles.Count != distinctRoleIds.Count)
            {
                throw new BusinessException(
                    SystemErrorCodes.RoleNotFound,
                    "One or more roles do not exist.");
            }
        }

        var existingLinks = await userRoleRepository.GetListAsync(link => link.UserId == userId);
        if (existingLinks.Count > 0)
        {
            await userRoleRepository.DeleteManyAsync(existingLinks);
        }

        foreach (var roleId in distinctRoleIds)
        {
            await userRoleRepository.InsertAsync(new UserRole
            {
                UserId = userId,
                RoleId = roleId
            });
        }
    }

    private async Task FillRolesAsync(List<UserDto> users)
    {
        if (users.Count == 0)
        {
            return;
        }

        var userIds = users.Select(user => user.Id).ToList();
        var links = await userRoleRepository.GetListAsync(link => userIds.Contains(link.UserId));
        if (links.Count == 0)
        {
            return;
        }

        var roleIds = links.Select(link => link.RoleId).Distinct().ToList();
        var roles = await roleRepository.GetListAsync(role => roleIds.Contains(role.Id));
        var roleById = roles.ToDictionary(role => role.Id);

        var rolesByUser = links
            .GroupBy(link => link.UserId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(link => roleById.GetValueOrDefault(link.RoleId))
                    .Where(role => role != null)
                    .Select(role => new RoleInfo
                    {
                        Id = role!.Id,
                        Code = role.Code,
                        Name = role.Name
                    })
                    .OrderBy(info => info.Name)
                    .ToList());

        foreach (var user in users)
        {
            user.Roles = rolesByUser.GetValueOrDefault(user.Id) ?? [];
        }
    }

    /// <summary>
    /// 角色分配等同于授予权限，必须单独校验角色管理权限，
    /// 避免只有 system:user:update 的操作者通过用户接口给自己或他人提权。
    /// </summary>
    private async Task EnsureCanAssignRolesAsync()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new AuthorizationException();

        var result = await authorizationService.AuthorizeAsync(
            httpContext.User,
            null,
            new PermissionAttribute(SystemPermissions.Role.Manage));

        if (!result.Succeeded)
        {
            throw new ForbiddenException(
                SystemErrorCodes.RoleManageRequired,
                "Assigning roles requires the role management permission.");
        }
    }

    private async Task<UserEntity?> EnsureUserIsDeletableAsync(Guid userId)
    {
        var user = await userRepository.GetAsync(userId);
        if (user?.IsBuiltIn == true)
        {
            throw CreateBuiltInAccountProtectedException(userId);
        }

        return user;
    }

    /// <summary>
    /// 内置账号只锁定身份：账号名与角色分配不可变，其余资料仍可编辑。
    /// </summary>
    private async Task EnsureBuiltInAccountPreservedAsync(UpdateUserDto input)
    {
        var user = await userRepository.GetAsync(input.Id);
        if (user?.IsBuiltIn != true)
        {
            return;
        }

        if (!string.Equals(user.UserName, input.UserName, StringComparison.Ordinal))
        {
            throw CreateBuiltInAccountProtectedException(input.Id);
        }

        if (input.RoleIds is not null)
        {
            var currentRoleIds = await GetUserRoleIdsAsync(input.Id);
            if (!input.RoleIds.OrderBy(id => id).SequenceEqual(currentRoleIds.OrderBy(id => id)))
            {
                throw CreateBuiltInAccountProtectedException(input.Id);
            }
        }
    }

    private static BusinessException CreateBuiltInAccountProtectedException(Guid userId) =>
        new BusinessException(
                SystemErrorCodes.BuiltInUserProtected,
                "The built-in account cannot be renamed, have its roles changed, have its password reset, or be deleted.")
            .WithData("UserId", userId);

    private async Task EnsureAdminRoleRetainedAsync(Guid userId, IReadOnlyCollection<Guid> roleIds)
    {
        var adminRole = await roleRepository.GetAsync(role => role.IsBuiltIn);
        if (adminRole == null)
        {
            return;
        }

        var currentLinks = await userRoleRepository.GetListAsync(link => link.UserId == userId);
        if (currentLinks.All(link => link.RoleId != adminRole.Id))
        {
            return;
        }

        if (roleIds.Contains(adminRole.Id))
        {
            return;
        }

        var otherLinks = await userRoleRepository.GetListAsync(
            link => link.RoleId == adminRole.Id && link.UserId != userId);
        var otherAdminIds = otherLinks.Select(link => link.UserId).Distinct().ToList();
        if (otherAdminIds.Count > 0)
        {
            var activeAdmins = await userRepository.GetListAsync(
                user => otherAdminIds.Contains(user.Id) && user.IsActive);
            if (activeAdmins.Count > 0)
            {
                return;
            }
        }

        throw new BusinessException(
            SystemErrorCodes.LastAdminRoleRemoval,
            "The last administrator must keep the Admin role.");
    }

    internal async Task EnsureUserIsUniqueAsync(string userName, string? email, Guid? excludedUserId = null)
    {
        // SqlSugar 5.1.4.216 无法翻译表达式内的 Nullable<T>.HasValue/Value 短路判断，
        // 因此在 C# 层分支后构造只包含普通比较的查询表达式。
        var hasExcludedUser = excludedUserId.HasValue;
        var excludedId = excludedUserId.GetValueOrDefault();

        var userNameTaken = hasExcludedUser
            ? await userRepository.GetAsync(user => user.UserName == userName && user.Id != excludedId)
            : await userRepository.GetAsync(user => user.UserName == userName);
        if (userNameTaken != null)
        {
            throw new BusinessException(SystemErrorCodes.UserNameAlreadyExists, "The user name is already taken.");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailTaken = hasExcludedUser
                ? await userRepository.GetAsync(user => user.Email == email && user.Id != excludedId)
                : await userRepository.GetAsync(user => user.Email == email);
            if (emailTaken != null)
            {
                throw new BusinessException(SystemErrorCodes.EmailAlreadyExists, "The email address is already registered.");
            }
        }
    }

    private static string HashRefreshToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequiredNickName(string? value) =>
        Normalize(value) ?? throw new BusinessException(
            SystemErrorCodes.NickNameRequired,
            "The nickname is required.");

    internal async Task EnsureNickNameIsUniqueAsync(string? nickName, Guid? excludedUserId = null)
    {
        var normalized = Normalize(nickName);
        if (normalized == null)
        {
            throw new BusinessException(
                SystemErrorCodes.NickNameRequired,
                "The nickname is required.");
        }

        var hasExcludedUser = excludedUserId.HasValue;
        var excludedId = excludedUserId.GetValueOrDefault();

        var nickNameTaken = hasExcludedUser
            ? await userRepository.GetAsync(user => user.NickName == normalized && user.Id != excludedId)
            : await userRepository.GetAsync(user => user.NickName == normalized);
        if (nickNameTaken != null)
        {
            throw new BusinessException(
                    SystemErrorCodes.NickNameAlreadyExists,
                    "The nickname is already taken.")
                .WithData("NickName", normalized);
        }
    }


    async Task<LoginUserDto> IExternalLoginAccountService.LoginAsync(string provider, ExternalLoginIdentity identity)
    {
        var providerKey = ExternalLoginSecurity.CreateProviderKeyHash(provider, identity.ProviderKey);
        var binding = await externalLoginRepository.GetAsync(provider, providerKey);
        var user = binding == null
            ? null
            : await userRepository.GetAsync(binding.UserId);

        if (user == null || binding == null)
        {
            throw new BusinessException(
                SystemErrorCodes.ExternalLoginNotLinked,
                "The external account is not linked to an active system user.");
        }

        if (!user.IsActive)
        {
            throw new BusinessException(
                SystemErrorCodes.ExternalLoginNotLinked,
                "The external account is not linked to an active system user.");
        }

        binding.ProviderUserName = identity.UserName ?? binding.ProviderUserName;
        binding.Email = identity.Email ?? binding.Email;
        await externalLoginRepository.UpdateAsync(binding);

        var twoFactorMethods = GetEnabledTwoFactorMethods(user);
        if (twoFactorMethods.Count > 0)
        {
            var policy = await GetVerificationPolicyAsync();
            CreateSecurityChallenge(user, "login", policy.CodeExpirationMinutes);
            await userRepository.UpdateAsync(user);
            return new LoginUserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                RequiresTwoFactor = true,
                TwoFactorChallengeId = user.SecurityChallengeId,
                TwoFactorMethods = twoFactorMethods
            };
        }

        return await IssueSessionAsync(user, null, null, null);
    }

    async Task<LoginUserDto> IExternalLoginAccountService.RegisterWithExternalLoginAsync(
        string provider,
        ExternalLoginIdentity identity,
        string userName,
        string password,
        string? nickName,
        string? email,
        string? phone)
    {
        var providerKey = ExternalLoginSecurity.CreateProviderKeyHash(provider, identity.ProviderKey);

        // 表单邮箱为空时沿用第三方身份邮箱，保持外部账号基本信息完整
        var resolvedEmail = Normalize(email) ?? Normalize(identity.Email);
        await EnsureUserIsUniqueAsync(userName, resolvedEmail);

        // 检查是否已在等待期间被绑定
        var existingBinding = await externalLoginRepository.GetAsync(provider, providerKey);
        if (existingBinding != null)
        {
            throw new BusinessException(
                SystemErrorCodes.ExternalLoginProviderError,
                "This external account has already been linked to another user.");
        }

        var resolvedNickName = Normalize(nickName ?? identity.DisplayName) ?? Normalize(userName);
        if (resolvedNickName is { Length: > 20 })
        {
            resolvedNickName = resolvedNickName[..20];
        }
        await EnsureNickNameIsUniqueAsync(resolvedNickName);

        // 创建用户
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            NickName = resolvedNickName,
            IsActive = true,
            Email = resolvedEmail,
            Phone = Normalize(phone),
            Description = "Created by external login registration."
        };
        SetPassword(user, password);
        await userRepository.InsertAsync(user);
        await AssignRegistrationRolesAsync(user.Id);
        await NotifyUserCreatedAsync(user);

        // 创建绑定
        var binding = new ExternalLoginEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = provider,
            ProviderKey = providerKey,
            ProviderUserName = identity.UserName,
            Email = identity.Email
        };
        await externalLoginRepository.InsertAsync(binding);

        return await SignInExternalUserAsync(user);
    }

    async Task<LoginUserDto> IExternalLoginAccountService.LinkWithExternalLoginAsync(
        string provider,
        ExternalLoginIdentity identity,
        string userName,
        string password)
    {
        var providerKey = ExternalLoginSecurity.CreateProviderKeyHash(provider, identity.ProviderKey);

        var existingBinding = await externalLoginRepository.GetAsync(provider, providerKey);
        if (existingBinding != null)
        {
            throw new BusinessException(
                SystemErrorCodes.ExternalLoginProviderError,
                "This external account has already been linked to another user.");
        }

        // 未绑定身份关联已有账号：先校验本系统账号密码，再写入绑定关系
        var user = await AuthenticateLocalUserAsync(userName, password);

        var binding = new ExternalLoginEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = provider,
            ProviderKey = providerKey,
            ProviderUserName = identity.UserName,
            Email = identity.Email
        };
        await externalLoginRepository.InsertAsync(binding);

        return await SignInExternalUserAsync(user);
    }

    /// <summary>
    /// 外部登录流程签发会话：开启 2FA 的账号仍需完成原有 challenge。
    /// </summary>
    private async Task<LoginUserDto> SignInExternalUserAsync(UserEntity user)
    {
        var twoFactorMethods = GetEnabledTwoFactorMethods(user);
        if (twoFactorMethods.Count > 0)
        {
            var policy = await GetVerificationPolicyAsync();
            CreateSecurityChallenge(user, "login", policy.CodeExpirationMinutes);
            await userRepository.UpdateAsync(user);
            return new LoginUserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                RequiresTwoFactor = true,
                TwoFactorChallengeId = user.SecurityChallengeId,
                TwoFactorMethods = twoFactorMethods
            };
        }

        return await IssueSessionAsync(user, null, null, null);
    }

}