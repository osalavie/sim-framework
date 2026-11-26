using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared.Data;
using Sim.System.Domain.Shared;
using Sim.System.Notifications;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.Permission;
using Sim.System.Domain.Role;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 权限数据种子
/// 按 <see cref="PermissionCatalog"/> 幂等补齐系统权限，并把历史遗留权限码规范化为 `module:resource:action` 形式。
/// </summary>
public class PermissionDataSeeder
{
    /// <summary>
    /// 历史权限码 → 现行权限码。
    /// 就地改名（而不是删除重建）以保留角色与权限的授权关系。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> LegacyPermissionCodeMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["System.Notification.Manage"] = NotificationPermissions.Manage,
            ["System.Notification.ViewDeliveries"] = NotificationPermissions.ViewDeliveries,
            ["sys:user:query"] = SystemPermissions.User.Query,
            ["sys:user:create"] = SystemPermissions.User.Create,
            ["sys:user:update"] = SystemPermissions.User.Update,
            ["sys:user:delete"] = SystemPermissions.User.Delete,
            ["sys:token:revoke-all"] = SystemPermissions.Token.RevokeAll,
            ["sys:token:cleanup"] = SystemPermissions.Token.Cleanup,
            ["sys:role:manage"] = SystemPermissions.Role.Manage,
            ["sys:role-module:manage"] = SystemPermissions.RoleModule.Manage,
            ["sys:permission:manage"] = SystemPermissions.Permission.Manage,
        };

    private readonly IPermissionRepository _permissionRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IEnumerable<IModuleSeedContributor> _moduleSeedContributors;
    private readonly ILogger<PermissionDataSeeder> _logger;

    public PermissionDataSeeder(
        IPermissionRepository permissionRepository,
        IRolePermissionRepository rolePermissionRepository,
        IEnumerable<IModuleSeedContributor> moduleSeedContributors,
        ILogger<PermissionDataSeeder> logger)
    {
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _moduleSeedContributors = moduleSeedContributors;
        _logger = logger;
    }

    /// <summary>
    /// 执行权限数据种子
    /// </summary>
    public async Task SeedAsync()
    {
        _logger.LogInformation("========== 开始创建权限数据 ==========");

        await RemoveLegacyGroupParameterPermissionAsync();
        await NormalizeLegacyPermissionCodesAsync();

        var definitions = PermissionCatalog.All
            .Concat(_moduleSeedContributors
                .SelectMany(contributor => contributor.Permissions)
                .Select(seed => new PermissionCatalog.PermissionDefinition(
                    seed.Code,
                    seed.Name,
                    seed.Description)))
            .GroupBy(definition => definition.Code, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        foreach (var definition in definitions)
        {
            await CreatePermissionIfNotExistsAsync(
                definition.Code,
                definition.Name,
                definition.Description);
        }

        _logger.LogInformation("========== 权限数据创建完成，共 {Count} 个权限 ==========", definitions.Count);
    }

    /// <summary>
    /// 移除已废弃的分组参数权限（历史遗留，权限码已不再使用）
    /// </summary>
    private async Task RemoveLegacyGroupParameterPermissionAsync()
    {
        var legacy = await _permissionRepository.GetAsync(permission =>
            permission.Code == "system:dictionary:manage");
        if (legacy == null)
        {
            return;
        }

        await _rolePermissionRepository.DeleteAsync(rolePermission =>
            rolePermission.PermissionId == legacy.Id);
        await _permissionRepository.DeleteAsync(permission => permission.Id == legacy.Id);
        _logger.LogInformation("已移除旧分组参数权限: {PermissionId}", legacy.Id);
    }

    /// <summary>
    /// 把历史权限码就地规范化为现行权限码；若现行权限码已存在，则移除历史记录避免重复。
    /// </summary>
    private async Task NormalizeLegacyPermissionCodesAsync()
    {
        foreach (var (legacyCode, currentCode) in LegacyPermissionCodeMap)
        {
            var legacyPermission = await _permissionRepository.GetAsync(p => p.Code == legacyCode);
            if (legacyPermission == null)
            {
                continue;
            }

            var currentPermission = await _permissionRepository.GetAsync(p => p.Code == currentCode);
            if (currentPermission != null)
            {
                await _rolePermissionRepository.DeleteAsync(rp => rp.PermissionId == legacyPermission.Id);
                await _permissionRepository.DeleteAsync(p => p.Id == legacyPermission.Id);
                _logger.LogInformation(
                    "移除重复的历史权限: {LegacyCode}（现行权限 {CurrentCode} 已存在）",
                    legacyCode,
                    currentCode);
                continue;
            }

            legacyPermission.Code = currentCode;
            await _permissionRepository.UpdateAsync(legacyPermission);
            _logger.LogInformation("规范化历史权限码: {LegacyCode} -> {CurrentCode}", legacyCode, currentCode);
        }
    }

    /// <summary>
    /// 创建权限（如果不存在）
    /// 支持幂等性：如果权限已存在，则跳过创建
    /// </summary>
    /// <param name="code">权限代码</param>
    /// <param name="name">权限名称</param>
    /// <param name="description">权限描述</param>
    private async Task CreatePermissionIfNotExistsAsync(
        string code,
        string name,
        string description)
    {
        var existingPermission = await _permissionRepository.GetAsync(p => p.Code == code);

        if (existingPermission != null)
        {
            _logger.LogDebug("权限已存在，跳过创建: {Code}", code);
            return;
        }

        var permission = new PermissionEntity
        {
            Code = code,
            Name = name,
            Description = description,
            PermissionType = PermissionTypeEnum.Button
        };

        await _permissionRepository.InsertAsync(permission);
        _logger.LogInformation("创建权限: {Code} - {Name} (ID: {Id})", code, name, permission.Id);
    }
}
