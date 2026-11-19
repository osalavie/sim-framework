using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.Tenant;
using Sim.System.Domain.ModuleConfig;
using Sim.System.Domain.Organization;
using Sim.System.Domain.Permission;
using Sim.System.Domain.Role;
using Sim.System.Domain.RouteConfig;
using Sim.System.Domain.RouteMeta;
using Sim.System.Domain.Security;
using Sim.System.Domain.Tenant;
using Sim.System.Domain.User;

namespace Sim.Admin.Domain.Services;

/// <summary>
/// 租户配置初始化服务 — 创建租户时完成完整初始化。
/// 放在 Admin.Domain 层因为需要访问 System 模块的实体与仓储
/// （User/Role/Permission/Module/Route/Tenant）。
/// 通过 ITenantProvisioningService 接口注入到 TenantAppService。
/// </summary>
public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IModuleConfigRepository _moduleConfigRepository;
    private readonly IRouteConfigRepository _routeConfigRepository;
    private readonly IRouteMetaRepository _routeMetaRepository;
    private readonly IModuleRouteRepository _moduleRouteRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IRoleModuleRepository _roleModuleRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ISqlSugarRepository<RoleDataScopeEntity> _roleDataScopeRepository;
    private readonly ISecurityManager _securityManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(
        TenantManager tenantManager,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IPermissionRepository permissionRepository,
        IModuleConfigRepository moduleConfigRepository,
        IRouteConfigRepository routeConfigRepository,
        IRouteMetaRepository routeMetaRepository,
        IModuleRouteRepository moduleRouteRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRoleModuleRepository roleModuleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ISqlSugarRepository<RoleDataScopeEntity> roleDataScopeRepository,
        ISecurityManager securityManager,
        IConfiguration configuration,
        ILogger<TenantProvisioningService> logger)
    {
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _permissionRepository = permissionRepository;
        _moduleConfigRepository = moduleConfigRepository;
        _routeConfigRepository = routeConfigRepository;
        _routeMetaRepository = routeMetaRepository;
        _moduleRouteRepository = moduleRouteRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _roleModuleRepository = roleModuleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _roleDataScopeRepository = roleDataScopeRepository;
        _securityManager = securityManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CreateTenantResultDto> ProvisionAsync(CreateTenantDto input)
    {
        _logger.LogInformation("========== 开始创建租户: {Name} ({Code}) ==========", input.Name, input.Code);

        // 1. 创建租户实体
        var tenant = await _tenantManager.CreateAsync(
            input.Name, input.Code, input.AdminEmail, input.ConnectionString);
        var tenantId = tenant.Id;

        _logger.LogInformation("租户创建成功: {TenantId}", tenantId);

        // 2. 复制选中的模块到租户名下
        var moduleIdMap = await CopyModulesAsync(tenantId, input.ModuleIds);

        // 3. 复制选中的路由（及路由元数据）到租户名下
        var routeIdMap = await CopyRoutesAsync(tenantId, input.RouteIds);

        // 3.1 复制模块-路由关联（多对多）
        await CopyModuleRoutesAsync(tenantId, moduleIdMap, routeIdMap);

        // 4. 复制选中的权限到租户名下
        var permissionIdMap = await CopyPermissionsAsync(tenantId, input.PermissionIds);

        // 5. 创建租户管理员用户（显式设置 TenantId，因为 Host 的 TenantContext 为 null）
        var (adminUser, adminPassword) = await CreateTenantAdminUserAsync(tenantId, input);

        // 6. 创建租户管理员角色并分配权限/模块
        var adminRole = await CreateTenantAdminRoleAsync(tenantId);

        // 7. 绑定权限到角色
        await AssignPermissionsToRoleAsync(tenantId, adminRole.Id, permissionIdMap.Values.ToList());

        // 8. 绑定模块到角色
        await AssignModulesToRoleAsync(tenantId, adminRole.Id, moduleIdMap.Values.ToList());

        // 9. 绑定管理员用户与角色
        await BindUserToRoleAsync(tenantId, adminUser.Id, adminRole.Id);

        _logger.LogInformation("========== 租户创建完成: {Name} ({Code}), 管理员: {AdminUserName} ==========",
            input.Name, input.Code, adminUser.UserName);

        return new CreateTenantResultDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            TenantCode = tenant.Code,
            AdminUserId = adminUser.Id,
            AdminUserName = adminUser.UserName,
            InitialPassword = adminPassword,
            AssignedModuleCount = moduleIdMap.Count,
            AssignedRouteCount = routeIdMap.Count,
            AssignedPermissionCount = permissionIdMap.Count
        };
    }

    public async Task DeleteTenantDataAsync(Guid tenantId)
    {
        var userIds = await _userRepository.GetQueryable()
            .Where(user => user.TenantId == tenantId)
            .Select(user => user.Id)
            .ToListAsync();
        if (userIds.Count > 0)
        {
            await _refreshTokenRepository.DeleteAsync(token => userIds.Contains(token.UserId));
            await _userRoleRepository.DeleteAsync(link => userIds.Contains(link.UserId));
            await _userRepository.DeleteAsync(user => userIds.Contains(user.Id));
        }

        var roleIds = await _roleRepository.GetQueryable()
            .Where(role => role.TenantId == tenantId)
            .Select(role => role.Id)
            .ToListAsync();
        if (roleIds.Count > 0)
        {
            await _rolePermissionRepository.DeleteAsync(link => roleIds.Contains(link.RoleId));
            await _roleModuleRepository.DeleteAsync(link => roleIds.Contains(link.RoleId));
            await _roleDataScopeRepository.DeleteAsync(scope => roleIds.Contains(scope.RoleId));
            await _roleRepository.DeleteAsync(role => roleIds.Contains(role.Id));
        }

        var moduleIds = await _moduleConfigRepository.GetQueryable()
            .Where(module => module.TenantId == tenantId)
            .Select(module => module.Id)
            .ToListAsync();
        var routeIds = await _routeConfigRepository.GetQueryable()
            .Where(route => route.TenantId == tenantId)
            .Select(route => route.Id)
            .ToListAsync();

        if (moduleIds.Count > 0 || routeIds.Count > 0)
        {
            await _moduleRouteRepository.DeleteAsync(link => link.TenantId == tenantId);
        }

        if (routeIds.Count > 0)
        {
            await _routeMetaRepository.DeleteAsync(meta => routeIds.Contains(meta.RouteConfigId));
            await _routeConfigRepository.DeleteAsync(route => routeIds.Contains(route.Id));
        }

        if (moduleIds.Count > 0)
        {
            await _moduleConfigRepository.DeleteAsync(module => moduleIds.Contains(module.Id));
        }

        await _permissionRepository.DeleteAsync(permission => permission.TenantId == tenantId);

        _logger.LogInformation(
            "Tenant {TenantId} identity/RBAC data deleted: users={UserCount}, roles={RoleCount}, modules={ModuleCount}, routes={RouteCount}",
            tenantId,
            userIds.Count,
            roleIds.Count,
            moduleIds.Count,
            routeIds.Count);
    }

    public async Task<TenantTemplateDto> GetTemplatesAsync()
    {
        // Host 的 TenantContext 为 null，ApplyTenantFilter 不会过滤。
        // 需要手动过滤 TenantId IS NULL 来获取 Host 级模板。
        var modules = await _moduleConfigRepository.GetListAsync(m => m.TenantId == null);
        var routes = await _routeConfigRepository.GetListAsync(r => r.TenantId == null);
        var permissions = await _permissionRepository.GetListAsync(p => p.TenantId == null);

        // 获取路由元数据
        var routeIds = routes.Select(r => r.Id).ToList();
        var metas = routeIds.Count > 0
            ? await _routeMetaRepository.GetListAsync(m => routeIds.Contains(m.RouteConfigId))
            : new List<RouteMetaEntity>();

        var metaDict = metas.ToDictionary(m => m.RouteConfigId);

        return new TenantTemplateDto
        {
            Modules = modules.OrderBy(m => m.Order).Select(m => new TemplateModuleDto
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                Icon = m.Icon,
                Order = m.Order,
                Enabled = m.Enabled
            }).ToList(),
            Routes = routes.Select(r => new TemplateRouteDto
            {
                Id = r.Id,
                ParentId = r.ParentId,
                Name = r.Name,
                Path = r.Path,
                Title = metaDict.TryGetValue(r.Id, out var meta) ? meta.Title : null,
                Icon = metaDict.TryGetValue(r.Id, out var meta2) ? meta2.Icon : null,
                Menu = metaDict.TryGetValue(r.Id, out var meta3) && meta3.Menu,
                Order = metaDict.TryGetValue(r.Id, out var meta4) ? meta4.Order : 0
            }).ToList(),
            Permissions = permissions.Select(p => new TemplatePermissionDto
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                Description = p.Description
            }).ToList()
        };
    }

    public async Task<TenantAssignmentDto> GetAssignmentsAsync(Guid tenantId)
    {
        var tenant = await _tenantRepository.GetAsync(tenantId)
            ?? throw new BusinessException(SystemErrorCodes.TenantNotFound, "Tenant not found.");

        // 临时切换 TenantContext 以查询租户数据
        // 因为 Host 的 TenantContext 为 null，需要手动设置才能通过 ApplyTenantFilter
        var originalTenantId = Sim.Framework.Domain.TenantContext.CurrentTenantId;
        try
        {
            Sim.Framework.Domain.TenantContext.CurrentTenantId = tenantId;

            var modules = await _moduleConfigRepository.GetAll();
            var routes = await _routeConfigRepository.GetAll();
            var permissions = await _permissionRepository.GetAll();

            var routeIds = routes.Select(r => r.Id).ToList();
            var metas = routeIds.Count > 0
                ? await _routeMetaRepository.GetListAsync(m => routeIds.Contains(m.RouteConfigId))
                : new List<RouteMetaEntity>();
            var metaDict = metas.ToDictionary(m => m.RouteConfigId);

            return new TenantAssignmentDto
            {
                TenantId = tenantId,
                TenantName = tenant.Name,
                Modules = modules.OrderBy(m => m.Order).Select(m => new TemplateModuleDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Description,
                    Icon = m.Icon,
                    Order = m.Order,
                    Enabled = m.Enabled
                }).ToList(),
                Routes = routes.Select(r => new TemplateRouteDto
                {
                    Id = r.Id,
                    ParentId = r.ParentId,
                    Name = r.Name,
                    Path = r.Path,
                    Title = metaDict.TryGetValue(r.Id, out var meta) ? meta.Title : null,
                    Icon = metaDict.TryGetValue(r.Id, out var meta2) ? meta2.Icon : null,
                    Menu = metaDict.TryGetValue(r.Id, out var meta3) && meta3.Menu,
                    Order = metaDict.TryGetValue(r.Id, out var meta4) ? meta4.Order : 0
                }).ToList(),
                Permissions = permissions.Select(p => new TemplatePermissionDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    Description = p.Description
                }).ToList()
            };
        }
        finally
        {
            Sim.Framework.Domain.TenantContext.CurrentTenantId = originalTenantId;
        }
    }

    /// <summary>
    /// 复制选中的模块到租户名下，返回 原ID → 新ID 的映射。
    /// </summary>
    private async Task<Dictionary<Guid, Guid>> CopyModulesAsync(Guid tenantId, List<Guid> moduleIds)
    {
        var idMap = new Dictionary<Guid, Guid>();
        if (moduleIds.Count == 0) return idMap;

        // 查询 Host 级模块（TenantId IS NULL）
        var modules = await _moduleConfigRepository.GetListAsync(
            m => moduleIds.Contains(m.Id) && m.TenantId == null);

        foreach (var module in modules)
        {
            var newId = Guid.NewGuid();
            var copy = new ModuleConfigEntity
            {
                Id = newId,
                TenantId = tenantId,
                Name = module.Name,
                Description = module.Description,
                Version = module.Version,
                Enabled = module.Enabled,
                Icon = module.Icon,
                Order = module.Order,
                Layout = module.Layout,
                EmptyRedirect = module.EmptyRedirect
            };
            await _moduleConfigRepository.InsertAsync(copy);
            idMap[module.Id] = newId;
            _logger.LogDebug("复制模块: {Name} ({OldId} → {NewId})", module.Name, module.Id, newId);
        }

        _logger.LogInformation("复制了 {Count} 个模块到租户 {TenantId}", idMap.Count, tenantId);
        return idMap;
    }

    /// <summary>
    /// 复制选中的路由（及元数据）到租户名下，返回 原ID → 新ID 的映射。
    /// </summary>
    private async Task<Dictionary<Guid, Guid>> CopyRoutesAsync(
        Guid tenantId, List<Guid> routeIds)
    {
        var idMap = new Dictionary<Guid, Guid>();
        if (routeIds.Count == 0) return idMap;

        var routes = await _routeConfigRepository.GetListAsync(
            r => routeIds.Contains(r.Id) && r.TenantId == null);

        // 获取路由元数据
        var routeMetaDict = new Dictionary<Guid, RouteMetaEntity>();
        if (routes.Count > 0)
        {
            var metas = await _routeMetaRepository.GetListAsync(
                m => routes.Select(r => r.Id).Contains(m.RouteConfigId));
            routeMetaDict = metas.ToDictionary(m => m.RouteConfigId);
        }

        // 第一遍：复制路由（生成新 ID），但 ParentId 暂时保留原值
        var routeOldToNew = new Dictionary<Guid, Guid>();
        var pendingParentUpdates = new List<(RouteConfigEntity copy, Guid? originalParentId)>();

        foreach (var route in routes)
        {
            var newId = Guid.NewGuid();

            var copy = new RouteConfigEntity
            {
                Id = newId,
                TenantId = tenantId,
                ParentId = route.ParentId, // 暂时保留，第二遍修正
                Name = route.Name,
                Path = route.Path,
                Component = route.Component,
                Redirect = route.Redirect
            };
            await _routeConfigRepository.InsertAsync(copy);

            routeOldToNew[route.Id] = newId;
            pendingParentUpdates.Add((copy, route.ParentId));

            // 复制路由元数据
            if (routeMetaDict.TryGetValue(route.Id, out var meta))
            {
                var metaCopy = new RouteMetaEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RouteConfigId = newId,
                    Title = meta.Title,
                    Icon = meta.Icon,
                    Menu = meta.Menu,
                    Tab = meta.Tab,
                    KeepAlive = meta.KeepAlive,
                    Pinned = meta.Pinned,
                    Anonymous = meta.Anonymous,
                    Disabled = meta.Disabled,
                    Order = meta.Order,
                    Module = meta.Module,
                    PermissionCode = meta.PermissionCode,
                    PermissionRequireAll = meta.PermissionRequireAll
                };
                await _routeMetaRepository.InsertAsync(metaCopy);
            }

            idMap[route.Id] = newId;
            _logger.LogDebug("复制路由: {Name} ({OldId} → {NewId})", route.Name, route.Id, newId);
        }

        // 第二遍：修正 ParentId（父子路由都在复制列表中的情况）
        foreach (var (copy, originalParentId) in pendingParentUpdates)
        {
            if (originalParentId.HasValue && routeOldToNew.TryGetValue(originalParentId.Value, out var newParentId))
            {
                copy.ParentId = newParentId;
                await _routeConfigRepository.UpdateAsync(copy);
            }
        }

        _logger.LogInformation("复制了 {Count} 个路由到租户 {TenantId}", idMap.Count, tenantId);
        return idMap;
    }

    /// <summary>
    /// 复制模块与路由的多对多关联，保留租户模块下发的路由集合。
    /// </summary>
    private async Task CopyModuleRoutesAsync(
        Guid tenantId,
        Dictionary<Guid, Guid> moduleIdMap,
        Dictionary<Guid, Guid> routeIdMap)
    {
        if (moduleIdMap.Count == 0 || routeIdMap.Count == 0)
        {
            return;
        }

        var moduleIds = moduleIdMap.Keys.ToList();
        var routeIds = routeIdMap.Keys.ToList();
        var links = await _moduleRouteRepository.GetListAsync(link =>
            link.TenantId == null &&
            moduleIds.Contains(link.ModuleId) &&
            routeIds.Contains(link.RouteId));

        foreach (var link in links)
        {
            await _moduleRouteRepository.InsertAsync(new ModuleRouteEntity
            {
                TenantId = tenantId,
                ModuleId = moduleIdMap[link.ModuleId],
                RouteId = routeIdMap[link.RouteId]
            });
        }

        _logger.LogInformation("复制了 {Count} 条模块路由关联到租户 {TenantId}", links.Count, tenantId);
    }

    /// <summary>
    /// 复制选中的权限到租户名下，返回 原ID → 新ID 的映射。
    /// </summary>
    private async Task<Dictionary<Guid, Guid>> CopyPermissionsAsync(Guid tenantId, List<Guid> permissionIds)
    {
        var idMap = new Dictionary<Guid, Guid>();
        if (permissionIds.Count == 0) return idMap;

        var permissions = await _permissionRepository.GetListAsync(
            p => permissionIds.Contains(p.Id) && p.TenantId == null);

        foreach (var perm in permissions)
        {
            var newId = Guid.NewGuid();
            var copy = new PermissionEntity
            {
                Id = newId,
                TenantId = tenantId,
                Name = perm.Name,
                Code = perm.Code,
                PermissionType = perm.PermissionType,
                Description = perm.Description
            };
            await _permissionRepository.InsertAsync(copy);
            idMap[perm.Id] = newId;
            _logger.LogDebug("复制权限: {Code} ({OldId} → {NewId})", perm.Code, perm.Id, newId);
        }

        _logger.LogInformation("复制了 {Count} 个权限到租户 {TenantId}", idMap.Count, tenantId);
        return idMap;
    }

    private static string GenerateRandomPassword() =>
        "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// 创建租户管理员用户。显式设置 TenantId（Host 的 TenantContext 为 null，AOP 不会自动注入）。
    /// </summary>
    private async Task<(UserEntity user, string password)> CreateTenantAdminUserAsync(
        Guid tenantId, CreateTenantDto input)
    {
        var userName = string.IsNullOrWhiteSpace(input.AdminUserName) ? "admin" : input.AdminUserName.Trim();
        var password = string.IsNullOrWhiteSpace(input.AdminPassword)
            ? GenerateRandomPassword()
            : input.AdminPassword;

        // 登录名在全站唯一，避免不同租户同名账号造成登录解析歧义。
        var existingUser = await _userRepository.GetAsync(user => user.UserName == userName);
        if (existingUser != null)
        {
            throw new BusinessException(
                SystemErrorCodes.UserNameAlreadyExists,
                $"The administrator user name '{userName}' is already in use.");
        }

        // 验证密码强度
        var (isValid, errors) = _securityManager.ValidatePassword(password);
        if (!isValid)
        {
            throw new BusinessException(
                SystemErrorCodes.TenantWeakPassword,
                $"租户管理员密码不符合安全要求: {string.Join(", ", errors)}");
        }

        var (encryptedPassword, salt) = _securityManager.EncryptPassword(password);
        var nickName = await BuildUniqueTenantAdminNickNameAsync(userName);

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId, // 显式设置
            UserName = userName,
            EncryptedPassword = encryptedPassword,
            Salt = salt,
            Email = input.AdminEmail,
            NickName = nickName,
            Name = userName,
            IsActive = true
        };
        await _userRepository.InsertAsync(user);

        _logger.LogInformation("租户管理员用户创建成功: {UserName} ({UserId}), TenantId={TenantId}",
            userName, user.Id, tenantId);

        return (user, password);
    }

    /// <summary>
    /// 昵称在全站唯一；租户管理员默认昵称不能固定为同一字符串。
    /// </summary>
    private async Task<string> BuildUniqueTenantAdminNickNameAsync(string userName)
    {
        var candidate = userName.Length <= 20 ? userName : userName[..20];
        var existing = await _userRepository.GetAsync(user => user.NickName == candidate);
        if (existing == null)
        {
            return candidate;
        }

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var prefixLength = Math.Min(candidate.Length, 13);
        return $"{candidate[..prefixLength]}-{suffix}";
    }

    /// <summary>
    /// 创建租户管理员角色。显式设置 TenantId。
    /// </summary>
    private async Task<RoleEntity> CreateTenantAdminRoleAsync(Guid tenantId)
    {
        var role = new RoleEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId, // 显式设置
            Name = "租户管理员",
            Code = RoleConst.Admin,
            Description = "租户管理员角色，拥有该租户内分配的全部权限",
            // 内置标记只属于宿主 Admin；租户管理员仅凭显式授予的权限工作。
            IsBuiltIn = false
        };
        await _roleRepository.InsertAsync(role);

        _logger.LogInformation("租户管理员角色创建成功: {RoleId}, TenantId={TenantId}", role.Id, tenantId);
        return role;
    }

    /// <summary>
    /// 将权限绑定到角色。显式设置 TenantId。
    /// </summary>
    private async Task AssignPermissionsToRoleAsync(Guid tenantId, Guid roleId, List<Guid> permissionIds)
    {
        foreach (var permissionId in permissionIds)
        {
            var rolePermission = new RolePermission
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RoleId = roleId,
                PermissionId = permissionId
            };
            await _rolePermissionRepository.InsertAsync(rolePermission);
        }

        _logger.LogInformation("为角色 {RoleId} 分配了 {Count} 个权限, TenantId={TenantId}",
            roleId, permissionIds.Count, tenantId);
    }

    /// <summary>
    /// 将模块绑定到角色。显式设置 TenantId。
    /// </summary>
    private async Task AssignModulesToRoleAsync(Guid tenantId, Guid roleId, List<Guid> moduleIds)
    {
        foreach (var moduleId in moduleIds)
        {
            var roleModule = new RoleModule
            {
                RoleId = roleId,
                ModuleId = moduleId,
                TenantId = tenantId
            };
            await _roleModuleRepository.InsertAsync(roleModule);
        }

        _logger.LogInformation("为角色 {RoleId} 分配了 {Count} 个模块, TenantId={TenantId}",
            roleId, moduleIds.Count, tenantId);
    }

    /// <summary>
    /// 绑定用户与角色。显式设置 TenantId。
    /// </summary>
    private async Task BindUserToRoleAsync(Guid tenantId, Guid userId, Guid roleId)
    {
        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId
        };
        await _userRoleRepository.InsertAsync(userRole);

        _logger.LogInformation("用户 {UserId} 绑定角色 {RoleId}, TenantId={TenantId}",
            userId, roleId, tenantId);
    }
}
