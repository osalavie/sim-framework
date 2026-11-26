namespace Sim.System.Domain.Shared.Consts;

/// <summary>
/// Data Protection 应用名与用途串。它们是持久化密文的解密契约，
/// 与展示品牌解耦；修改后历史密文将无法解密，必须配套双读迁移或密文重录方案。
/// </summary>
public static class DataProtectionIdentity
{
    /// <summary>Data Protection 应用名。</summary>
    public const string ApplicationName = "SimApp";

    public const string FileStorageConfigurationPurpose =
        ApplicationName + ".System.FileStorageConfiguration.v1";

    public const string NotificationConfigurationPurpose =
        ApplicationName + ".System.NotificationConfiguration.v1";

    public const string ScheduleTaskConfigurationPurpose =
        ApplicationName + ".System.ScheduleTaskConfiguration.v1";

    public const string UserSecuritySecretsPurpose = "Sim.System.UserSecuritySecrets.v1";
}
