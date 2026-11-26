using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.User;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.User;

namespace Sim.System.Application.User;

/// <summary>
/// 当前登录用户的前端偏好。读取不到记录时返回 null（不写库），
/// 只有用户真正切换语言/明暗/主题色后才由前端写入，避免替用户生成默认偏好。
/// </summary>
[Authorize]
public sealed class UserPreferenceService(
    ISqlSugarRepository<UserPreferenceEntity> repository)
    : ApplicationService, IUserPreferenceService
{
    /// <summary>
    /// 获取当前用户的前端偏好；尚未设置时返回空。
    /// </summary>
    public async Task<UserPreferenceDto?> GetMyPreferencesAsync()
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var entity = await repository.GetAsync(preference => preference.UserId == userId);
        return entity is null ? null : Map(entity);
    }

    /// <summary>
    /// 更新当前用户的前端偏好。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<UserPreferenceDto> UpdateMyPreferencesAsync(UpdateUserPreferenceDto input)
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var entity = await repository.GetAsync(preference => preference.UserId == userId);
        if (entity is null)
        {
            entity = new UserPreferenceEntity(Guid.NewGuid())
            {
                UserId = userId,
                Language = UserPreferenceDefaults.Language,
                ThemeMode = UserPreferenceDefaults.ThemeMode,
                ThemeStyle = UserPreferenceDefaults.ThemeStyle
            };
            Apply(entity, input);
            await repository.InsertAsync(entity);
        }
        else
        {
            Apply(entity, input);
            await repository.UpdateAsync(entity);
        }

        return Map(entity);
    }

    private static void Apply(UserPreferenceEntity entity, UpdateUserPreferenceDto input)
    {
        if (!string.IsNullOrWhiteSpace(input.Language))
        {
            var language = input.Language.Trim();
            if (UserPreferenceDefaults.SupportedLanguages.Contains(language))
            {
                entity.Language = language;
            }
        }

        if (!string.IsNullOrWhiteSpace(input.ThemeMode))
        {
            var themeMode = input.ThemeMode.Trim().ToLowerInvariant();
            if (UserPreferenceDefaults.SupportedThemeModes.Contains(themeMode))
            {
                entity.ThemeMode = themeMode;
            }
        }

        var themeStyle = NormalizeThemeStyle(input.ThemeStyle);
        if (themeStyle is not null)
        {
            entity.ThemeStyle = themeStyle;
        }
    }

    /// <summary>
    /// 主题风格由前端注册表演进，后端只约束长度与字符集，
    /// 避免新增风格时必须发布后端。
    /// </summary>
    private static string? NormalizeThemeStyle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var themeStyle = value.Trim();
        if (themeStyle.Length > UserPreferenceDefaults.MaxThemeStyleLength)
        {
            return null;
        }

        foreach (var character in themeStyle)
        {
            var valid = char.IsAsciiLetterOrDigit(character) || character == '-' || character == '_';
            if (!valid)
            {
                return null;
            }
        }

        return themeStyle;
    }

    private static UserPreferenceDto Map(UserPreferenceEntity entity) => new()
    {
        Language = entity.Language,
        ThemeMode = entity.ThemeMode,
        ThemeStyle = entity.ThemeStyle
    };
}
