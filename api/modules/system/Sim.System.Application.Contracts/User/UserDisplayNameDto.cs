namespace Sim.System.Application.Contracts.User;

/// <summary>
/// 创建人/修改人的展示信息：显示名（昵称优先，昵称为空时回退账号名）与账号名。
/// </summary>
public class UserDisplayNameDto
{
    /// <summary>列表单元格展示的名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>账号名，用于悬停气泡展示。</summary>
    public string UserName { get; set; } = string.Empty;
}
