using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.User;

/// <summary>
/// 当前登录用户的前端偏好（语言、明暗模式、主题风格）。
/// 未保存过偏好时返回 null，由前端保留本机选择。
/// </summary>
public interface IUserPreferenceService : IApplicationService
{
    Task<UserPreferenceDto?> GetMyPreferencesAsync();

    Task<UserPreferenceDto> UpdateMyPreferencesAsync(UpdateUserPreferenceDto input);
}
