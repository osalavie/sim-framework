using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sim.System.Application.SystemConfig;
using Sim.System.Domain.Security;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.Security;

namespace Sim.System.Application.Security;

/// <summary>
/// 密码策略优先读取系统设置，缺失或非法时回退 <see cref="SecurityOptions"/>。
/// </summary>
public sealed class PasswordPolicyProvider(
    ISystemConfigurationReader configurationReader,
    IOptions<SecurityOptions> securityOptions) : IPasswordPolicyProvider
{
    public async Task<PasswordPolicy> GetAsync()
    {
        var values = await configurationReader.GetValuesAsync(
            SystemConfigurationKeys.PasswordMinLength,
            SystemConfigurationKeys.PasswordRequireUppercase,
            SystemConfigurationKeys.PasswordRequireLowercase,
            SystemConfigurationKeys.PasswordRequireDigit,
            SystemConfigurationKeys.PasswordRequireSpecialChar);

        var options = securityOptions.Value;
        return new PasswordPolicy(
            ReadInt(
                values.GetValueOrDefault(SystemConfigurationKeys.PasswordMinLength),
                8, 128, options.PasswordMinLength),
            ReadBool(
                values.GetValueOrDefault(SystemConfigurationKeys.PasswordRequireUppercase),
                options.PasswordRequireUppercase),
            ReadBool(
                values.GetValueOrDefault(SystemConfigurationKeys.PasswordRequireLowercase),
                options.PasswordRequireLowercase),
            ReadBool(
                values.GetValueOrDefault(SystemConfigurationKeys.PasswordRequireDigit),
                options.PasswordRequireDigit),
            ReadBool(
                values.GetValueOrDefault(SystemConfigurationKeys.PasswordRequireSpecialChar),
                options.PasswordRequireSpecialChar));
    }

    private static int ReadInt(string? value, int min, int max, int fallback) =>
        int.TryParse(value, out var parsed) && parsed >= min && parsed <= max ? parsed : fallback;

    private static bool ReadBool(string? value, bool fallback) =>
        bool.TryParse(value, out var parsed) ? parsed : fallback;
}
