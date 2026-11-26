using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sim.Framework.Core;
using Sim.SqlSugar.Core;
using Sim.System.Domain.Shared;
using Volo.Abp.Modularity;
using Sim.System.Domain.Security;
using Sim.System.Domain.User;

namespace Sim.System.Domain;

[DependsOn(
    typeof(SystemDomainSharedModule),
    typeof(FrameworkCoreModule),
    typeof(SqlSugarCoreModule)
)]
public class SystemDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var securitySection = configuration.GetSection(SecurityOptions.SectionName);

        context.Services.AddTransient<ISecurityManager, SecurityManager>();

        context.Services.AddOptions<SecurityOptions>()
            .Bind(securitySection)
            .Validate(options => options.PasswordMinLength >= 8, "Security:PasswordMinLength must be at least 8.")
            .Validate(options => options.PasswordHashIterations >= 100_000,
                "Security:PasswordHashIterations must be at least 100000.")
            .Validate(options => options.LoginFailureLockoutThreshold > 0,
                "Security:LoginFailureLockoutThreshold must be greater than zero.")
            .Validate(options => options.LockoutDurationMinutes > 0,
                "Security:LockoutDurationMinutes must be greater than zero.")
            .Validate(options => options.RefreshTokenExpirationDays > 0,
                "Security:RefreshTokenExpirationDays must be greater than zero.")
            .Validate(options => options.SessionTimeoutMinutes > 0,
                "Security:SessionTimeoutMinutes must be greater than zero.")
            .Validate(options =>
                    options.DeviceSessionLimits.Pc is >= 0 and <= 20 &&
                    options.DeviceSessionLimits.Pad is >= 0 and <= 20 &&
                    options.DeviceSessionLimits.Phone is >= 0 and <= 20,
                "Security:DeviceSessionLimits values must be between 0 and 20.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.RefreshTokenCookieName),
                "Security:RefreshTokenCookieName must not be empty.")
            .Validate(options => options.VerificationCodeExpirationMinutes is >= 1 and <= 15,
                "Security:VerificationCodeExpirationMinutes must be between 1 and 15.")
            .Validate(options => options.VerificationCodeMaxAttempts is >= 3 and <= 10,
                "Security:VerificationCodeMaxAttempts must be between 3 and 10.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AuthenticatorIssuer),
                "Security:AuthenticatorIssuer must not be empty.")
            .ValidateOnStart();
        context.Services.Configure<PasswordHasherOptions>(options =>
        {
            options.IterationCount = securitySection.GetValue<int>(
                nameof(SecurityOptions.PasswordHashIterations),
                210_000);
        });

        context.Services.AddScoped<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();
    }
}
