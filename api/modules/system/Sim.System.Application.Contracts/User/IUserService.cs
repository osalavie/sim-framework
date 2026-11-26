using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.User;

public interface IUserService : ICustomCrudAppService<Guid, UserDto, CreateUserDto, UpdateUserDto>
{
    Task<UserDto> GetMyProfileAsync();

    Task<UserDto> UpdateMyProfileAsync(UpdateMyProfileDto input);

    Task<UserDto> UploadMyAvatarAsync(
        IRemoteStreamContent file,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取浏览器会话请求使用的防伪令牌
    /// </summary>
    Task<AntiforgeryTokenDto> GetAntiforgeryTokenAsync();

    /// <summary>
    /// 用户注册
    /// </summary>
    Task<UserDto> RegisterAsync(CreateUserDto input);
    
    /// <summary>
    /// 用户登录
    /// </summary>
    Task<LoginUserDto> LoginAsync(LoginRequest loginRequest);

    Task SendTwoFactorCodeAsync(TwoFactorCodeRequest input);

    Task<LoginUserDto> CompleteTwoFactorLoginAsync(CompleteTwoFactorLoginRequest input);

    Task ChangePasswordAsync(ChangePasswordRequest input);

    /// <summary>
    /// 当前登录用户主动注销账号
    /// </summary>
    Task PostDeleteMyAccountAsync(DeleteMyAccountRequest input);

    Task<PasswordResetChallengeDto> RequestPasswordResetAsync(PasswordResetRequest input);

    Task ResetPasswordAsync(ResetPasswordRequest input);

    /// <summary>
    /// 将指定用户的密码重置为系统配置的用户默认密码
    /// </summary>
    Task ResetUserPasswordAsync(Guid id);

    Task<TwoFactorStatusDto> GetTwoFactorStatusAsync();

    Task<AuthenticatorSetupDto> BeginAuthenticatorSetupAsync(BeginAuthenticatorSetupRequest input);

    Task<AuthenticatorRecoveryCodesDto> ConfirmAuthenticatorSetupAsync(
        ConfirmAuthenticatorSetupRequest input);

    Task<PasswordResetChallengeDto> RequestMessageTwoFactorSetupAsync(
        MessageTwoFactorSetupRequest input);

    Task ConfirmMessageTwoFactorSetupAsync(ConfirmMessageTwoFactorSetupRequest input);

    Task DisableTwoFactorAsync(DisableTwoFactorRequest input);
    
    /// <summary>
    /// 刷新访问令牌
    /// </summary>
    Task<LoginUserDto> RefreshTokenAsync();
    
    /// <summary>
    /// 撤销刷新令牌
    /// </summary>
    Task RevokeRefreshTokenAsync();
    
    /// <summary>
    /// 撤销用户的所有刷新令牌
    /// </summary>
    Task RevokeAllRefreshTokensAsync(Guid userId);

    Task<IReadOnlyList<LoginSessionDto>> GetMyLoginSessionsAsync();

    Task RevokeMyLoginSessionAsync(Guid sessionId);
}
