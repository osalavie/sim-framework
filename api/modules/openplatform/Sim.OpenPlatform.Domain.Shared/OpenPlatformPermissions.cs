namespace Sim.OpenPlatform.Domain.Shared;

public static class OpenPlatformPermissions
{
    /// <summary>管理开放平台应用（查看、创建、更新、启停、重置密钥、删除）</summary>
    public const string Manage = "openplatform:manage";

    /// <summary>查看开放平台访问日志</summary>
    public const string ViewAccessLogs = "openplatform:access-log:view";
}