using System.Threading.Tasks;

namespace Sim.System.Domain.Shared.Security;

/// <summary>当前生效的密码复杂度策略。</summary>
public sealed record PasswordPolicy(
    int MinLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireDigit,
    bool RequireSpecialChar);

/// <summary>读取当前租户作用域下的密码策略。</summary>
public interface IPasswordPolicyProvider
{
    Task<PasswordPolicy> GetAsync();
}
