namespace Sim.Admin.Domain.Data;
/// <summary>
/// 数据种子常量配置
/// </summary>
public static class DataSeedConsts
{
    /// <summary>
    /// 超级管理员用户名
    /// </summary>
    public const string AdminUserName = "admin";

    /// <summary>
    /// 超级管理员邮箱
    /// </summary>
    public const string AdminEmail = "admin@simadmin.com";

    /// <summary>
    /// 系统账号用户名。系统自动创建的种子数据统一以该账号作为创建人，账号不可登录。
    /// </summary>
    public const string SystemUserName = "system";

    /// <summary>
    /// 系统账号显示名称。
    /// </summary>
    public const string SystemUserDisplayName = "系统";

    /// <summary>
    /// 超级权限代码
    /// </summary>
    public const string SuperPermissionCode = "*:*:*";

    /// <summary>
    /// 系统初始化状态配置键。行存在时表示站点仍处于首次初始化流程。
    /// </summary>
    public const string SetupStateConfigKey = "system.setup.state";

    /// <summary>
    /// 初始化状态：等待首次启动设置。
    /// </summary>
    public const string SetupStatePending = "pending";

    /// <summary>
    /// 初始化状态：已完成。
    /// </summary>
    public const string SetupStateCompleted = "completed";
}
