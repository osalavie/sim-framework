using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using Sim.System.Domain.Shared.Consts;
using SqlSugar;

namespace Sim.System.Domain.User;

/// <summary>
/// 登录用户的前端偏好：界面语言、明暗模式与主题风格。
/// 每个用户最多一条记录；没有记录时前端保留本机选择，不做任何覆盖。
/// </summary>
[CodeFirst]
[SugarTable("system_user_preference")]
[SugarIndex("uidx_user_preference_user_id", nameof(UserId), OrderByType.Asc, true)]
public sealed class UserPreferenceEntity : AuditedAggregateRoot
{
    public UserPreferenceEntity()
    {
    }

    public UserPreferenceEntity(Guid id) : base(id)
    {
    }

    public Guid UserId { get; set; }

    [SugarColumn(Length = UserPreferenceDefaults.MaxLanguageLength, IsNullable = false)]
    public string Language { get; set; } = UserPreferenceDefaults.Language;

    [SugarColumn(Length = UserPreferenceDefaults.MaxThemeModeLength, IsNullable = false)]
    public string ThemeMode { get; set; } = UserPreferenceDefaults.ThemeMode;

    [SugarColumn(Length = UserPreferenceDefaults.MaxThemeStyleLength, IsNullable = false)]
    public string ThemeStyle { get; set; } = UserPreferenceDefaults.ThemeStyle;
}
