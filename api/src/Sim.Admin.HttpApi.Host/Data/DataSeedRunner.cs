using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sim.Admin.Domain.Data;
using Sim.Framework.Domain.Shared.Auditing;
using Sim.Framework.Domain.Shared.Data;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Role;
using Sim.System.Domain.User;

namespace Sim.Admin.HttpApi.Host.Data;

/// <summary>
/// 统一的种子执行入口。系统未完成首次初始化时跳过；
/// 初始化完成后以不可登录的系统账号作为审计归属人执行种子。
/// </summary>
public class DataSeedRunner
{
    private readonly IEnumerable<IDataSeedContributor> _seedContributors;
    private readonly AdminRoleAssignmentDataSeeder _adminRoleAssignmentDataSeeder;
    private readonly UserRoleAssignmentDataSeeder _userRoleAssignmentDataSeeder;
    private readonly SetupStateReader _setupStateReader;
    private readonly SystemUserDataSeeder _systemUserDataSeeder;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<DataSeedRunner> _logger;

    public DataSeedRunner(
        IEnumerable<IDataSeedContributor> seedContributors,
        AdminRoleAssignmentDataSeeder adminRoleAssignmentDataSeeder,
        UserRoleAssignmentDataSeeder userRoleAssignmentDataSeeder,
        SetupStateReader setupStateReader,
        SystemUserDataSeeder systemUserDataSeeder,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IUserRepository userRepository,
        ILogger<DataSeedRunner> logger)
    {
        _seedContributors = seedContributors;
        _adminRoleAssignmentDataSeeder = adminRoleAssignmentDataSeeder;
        _userRoleAssignmentDataSeeder = userRoleAssignmentDataSeeder;
        _setupStateReader = setupStateReader;
        _systemUserDataSeeder = systemUserDataSeeder;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// 在站点已完成初始化的前提下执行种子。
    /// </summary>
    /// <returns>是否实际执行了种子。</returns>
    public async Task<bool> RunIfInitializedAsync()
    {
        if (!await _setupStateReader.IsInitializedAsync())
        {
            _logger.LogInformation("系统尚未完成初始化，跳过数据种子；初始化提交时会以系统账号身份执行");
            return false;
        }

        var administratorId = await ResolveAdministratorIdAsync();
        UserEntity systemUser;
        IDisposable? administratorScope = administratorId.HasValue
            ? AuditContext.UseUser(administratorId.Value)
            : null;

        try
        {
            systemUser = await _systemUserDataSeeder.EnsureAsync();
        }
        finally
        {
            administratorScope?.Dispose();
        }

        using var seedScope = AuditContext.UseUser(systemUser.Id);
        foreach (var contributor in _seedContributors)
        {
            await contributor.SeedAsync();
        }

        await _adminRoleAssignmentDataSeeder.SeedAsync();
        // 模块自描述种子（如审批编号定义）在平台模块种子之后执行。
        await _userRoleAssignmentDataSeeder.SeedAsync();

        return true;
    }

    private async Task<Guid?> ResolveAdministratorIdAsync()
    {
        var role = await _roleRepository.GetAsync(item => item.IsBuiltIn);
        if (role == null)
        {
            _logger.LogWarning("未找到管理员角色，数据种子将不记录创建人");
            return null;
        }

        var links = await _userRoleRepository.GetListAsync(link => link.RoleId == role.Id);
        if (links.Count == 0)
        {
            _logger.LogWarning("未找到管理员账号，数据种子将不记录创建人");
            return null;
        }

        var userIds = links.Select(link => link.UserId).ToArray();
        var users = await _userRepository.GetListAsync(user => userIds.Contains(user.Id));
        var administrator = users.OrderBy(user => user.CreationTime).FirstOrDefault();
        if (administrator == null)
        {
            _logger.LogWarning("未找到管理员账号，数据种子将不记录创建人");
        }

        return administrator?.Id;
    }
}
