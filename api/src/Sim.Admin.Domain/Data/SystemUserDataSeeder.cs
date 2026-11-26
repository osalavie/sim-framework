using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sim.System.Domain.User;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 系统账号种子。系统自动创建的数据统一以该账号作为创建人，
/// 账号本身保持停用且不设置密码，不能用于登录。
/// </summary>
public class SystemUserDataSeeder
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SystemUserDataSeeder> _logger;

    public SystemUserDataSeeder(
        IUserRepository userRepository,
        ILogger<SystemUserDataSeeder> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// 幂等确保系统账号存在，并返回该账号。
    /// </summary>
    public async Task<UserEntity> EnsureAsync()
    {
        var existing = await _userRepository.GetAsync(user =>
            user.UserName == DataSeedConsts.SystemUserName);
        if (existing != null)
        {
            if (existing.IsActive)
            {
                _logger.LogWarning(
                    "用户名 {UserName} 已被启用账号占用，系统数据创建人将沿用该账号",
                    DataSeedConsts.SystemUserName);
            }
            else if (!existing.IsBuiltIn)
            {
                existing.IsBuiltIn = true;
                await _userRepository.UpdateAsync(existing);
            }

            return existing;
        }

        var systemUser = new UserEntity
        {
            UserName = DataSeedConsts.SystemUserName,
            Name = DataSeedConsts.SystemUserDisplayName,
            NickName = DataSeedConsts.SystemUserDisplayName,
            IsActive = false,
            IsBuiltIn = true
        };

        await _userRepository.InsertAsync(systemUser);
        _logger.LogInformation(
            "系统账号已创建: {UserName}, ID: {UserId}",
            systemUser.UserName,
            systemUser.Id);
        return systemUser;
    }
}
