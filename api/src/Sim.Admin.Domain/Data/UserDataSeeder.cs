using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Role;
using Sim.System.Domain.Security;
using Sim.System.Domain.SystemConfig;
using Sim.System.Domain.User;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 用户数据种子
/// 负责在首次启动时为默认管理员账号提供种子数据。
/// 默认账号只是初始化前的占位：站点完成初始化后，账号与密码以初始化设置为准。
/// </summary>
public class UserDataSeeder
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ISecurityManager _securityManager;
    private readonly ISqlSugarRepository<SystemConfigurationItemEntity> _configurationRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserDataSeeder> _logger;

    public UserDataSeeder(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        ISecurityManager securityManager,
        ISqlSugarRepository<SystemConfigurationItemEntity> configurationRepository,
        IConfiguration configuration,
        ILogger<UserDataSeeder> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _securityManager = securityManager;
        _configurationRepository = configurationRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 执行用户数据种子
    /// </summary>
    public async Task SeedAsync()
    {
        _logger.LogInformation("========== 开始创建基础用户 ==========");

        var setupState = await GetSetupStateAsync();
        if (string.Equals(setupState, DataSeedConsts.SetupStateCompleted, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("系统已完成初始化，跳过默认管理员账号种子");
            return;
        }

        var existingUser = await _userRepository.GetAsync(u =>
            u.UserName == DataSeedConsts.AdminUserName);
        if (existingUser != null)
        {
            _logger.LogDebug("默认管理员已存在，检查角色分配: {UserName}", existingUser.UserName);
            await EnsureUserRoleAsync(existingUser.Id, RoleConst.Admin);
            return;
        }

        if (setupState == null || string.Equals(
                setupState,
                DataSeedConsts.SetupStatePending,
                StringComparison.OrdinalIgnoreCase))
        {
            // 已有用户说明站点正在初始化（管理员已创建）或属于升级前的历史站点，
            // 此时不再补默认占位账号，避免和初始化设置的账号重复。
            if (await _userRepository.GetQueryable().AnyAsync())
            {
                _logger.LogInformation("检测到既有用户，跳过默认管理员账号种子");
                return;
            }
        }

        if (setupState == null)
        {
            await UpsertSetupStateAsync(DataSeedConsts.SetupStatePending);
        }

        var password = _configuration["App:AdminDefaultPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation(
                "未配置 App:AdminDefaultPassword，默认管理员账号将在 Web 系统初始化时创建");
            return;
        }

        // 验证密码强度
        var (isValid, errors, _) = await _securityManager.ValidatePasswordAsync(password);
        if (!isValid)
        {
            throw new InvalidOperationException(
                $"种子用户 {DataSeedConsts.AdminUserName} 的密码不符合安全要求: {string.Join(", ", errors)}");
        }

        // 加密密码
        var (encryptedPassword, salt) = _securityManager.EncryptPassword(password);

        // 创建用户
        var user = new UserEntity
        {
            UserName = DataSeedConsts.AdminUserName,
            EncryptedPassword = encryptedPassword,
            Salt = salt,
            Email = DataSeedConsts.AdminEmail,
            NickName = "系统管理员",
            Name = DataSeedConsts.AdminUserName,
            IsBuiltIn = true
        };

        await _userRepository.InsertAsync(user);
        _logger.LogInformation(
            "默认管理员账号已创建: {UserName}, ID: {UserId}，首次初始化时可直接修改账号与密码",
            user.UserName,
            user.Id);

        // 分配角色
        await AssignRoleToUserAsync(user.Id, user.UserName, RoleConst.Admin);
    }

    private async Task<string?> GetSetupStateAsync()
    {
        var item = await _configurationRepository.GetAsync(item =>
            item.TenantScope == "host" &&
            item.GroupKey == "system.setup" &&
            item.ConfigKey == DataSeedConsts.SetupStateConfigKey);
        return item?.Value;
    }

    private async Task UpsertSetupStateAsync(string state)
    {
        var existing = await _configurationRepository.GetAsync(item =>
            item.TenantScope == "host" &&
            item.GroupKey == "system.setup" &&
            item.ConfigKey == DataSeedConsts.SetupStateConfigKey);
        if (existing != null)
        {
            return;
        }

        await _configurationRepository.InsertAsync(new SystemConfigurationItemEntity(Guid.NewGuid())
        {
            TenantScope = "host",
            GroupKey = "system.setup",
            GroupName = "系统初始化",
            ConfigKey = DataSeedConsts.SetupStateConfigKey,
            ConfigName = "初始化状态",
            Value = state,
            Description = "pending 表示等待首次启动设置，completed 表示已完成初始化",
            Sort = 10,
            Enabled = true
        });
    }

    /// <summary>
    /// 确保用户拥有指定角色
    /// 如果用户角色关联已存在，则跳过；否则创建关联
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleCode">角色代码</param>
    private async Task EnsureUserRoleAsync(Guid userId, string roleCode)
    {
        // 获取角色
        var role = await _roleRepository.GetAsync(r => r.Code == roleCode);
        if (role == null)
        {
            var errorMsg = $"角色不存在: {roleCode}，无法为用户分配角色";
            _logger.LogError(errorMsg);
            throw new Exception(errorMsg);
        }

        // 检查用户角色关联是否已存在
        var existingUserRole = await _userRoleRepository.GetAsync(ur => 
            ur.UserId == userId && ur.RoleId == role.Id);
        
        if (existingUserRole != null)
        {
            _logger.LogDebug("用户已拥有角色: UserId={UserId}, RoleCode={RoleCode}", userId, roleCode);
            return;
        }

        // 创建用户角色关联
        var userRole = new UserRole
        {
            UserId = userId,
            RoleId = role.Id
        };
        
        await _userRoleRepository.InsertAsync(userRole);
        _logger.LogInformation("已为用户分配角色: UserId={UserId}, RoleCode={RoleCode}, RoleId={RoleId}", 
            userId, roleCode, role.Id);
    }

    /// <summary>
    /// 为用户分配角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="username">用户名（用于日志）</param>
    /// <param name="roleCode">角色代码</param>
    private async Task AssignRoleToUserAsync(Guid userId, string username, string roleCode)
    {
        // 获取角色
        var role = await _roleRepository.GetAsync(r => r.Code == roleCode);
        if (role == null)
        {
            var errorMsg = $"角色不存在: {roleCode}，无法为用户 {username} 分配角色";
            _logger.LogError(errorMsg);
            throw new Exception(errorMsg);
        }

        // 创建用户角色关联
        var userRole = new UserRole
        {
            UserId = userId,
            RoleId = role.Id
        };
        
        try
        {
            await _userRoleRepository.InsertAsync(userRole);
            _logger.LogInformation("已为用户 {UserName} 分配角色 {RoleCode}, RoleId: {RoleId}", 
                username, roleCode, role.Id);
        }
        catch (Exception ex)
        {
            var errorMsg = $"为用户 {username} 分配角色 {roleCode} 失败";
            _logger.LogError(ex, errorMsg);
            throw new Exception(errorMsg, ex);
        }
    }
}
