using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Sim.System.Application.Contracts.ModuleConfig;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Domain.Shared.Security;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Application.Contracts.Dtos;
using Sim.Framework.Core.UnitOfWork;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.ModuleConfig;
using Sim.System.Domain.Role;
using Sim.System.Domain.RouteConfig;
using Sim.System.Domain.User;

namespace Sim.System.Application.ModuleConfig;

/// <summary>
/// Module configuration application service implementation
/// </summary>
[Authorize]
public class ModuleConfigAppService(IModuleConfigRepository moduleRepository,
        IRouteConfigRepository routeRepository,
        IUserRoleRepository userRoleRepository,
        IRoleModuleRepository roleModuleRepository,
        IRoleRepository roleRepository,
        IModuleRouteRepository moduleRouteRepository,
        ICurrentUser currentUser,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor) : CrudAppService<ModuleConfigEntity,ModuleConfigDto,CreateModuleConfigDto,UpdateModuleConfigDto>(moduleRepository), IModuleConfigAppService
{
    /// <summary>
    /// Get modules accessible by the current user based on their roles
    /// If user has Admin role, return all enabled modules
    /// </summary>
    public async Task<List<ModuleConfigDto>> GetUserModulesAsync()
    {
        // 1. Check if user is authenticated
        if (!currentUser.IsAuthenticated || !currentUser.Id.HasValue)
        {
            return new List<ModuleConfigDto>();
        }

        var userId = currentUser.Id.Value;

        // 2. Get user's roles
        var userRoles = await userRoleRepository.GetListAsync(ur => ur.UserId == userId);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();

        if (!roleIds.Any())
        {
            return new List<ModuleConfigDto>();
        }

        // 3. Check if user has Admin role
        var roles = await roleRepository.GetListAsync(r => roleIds.Contains(r.Id));
        var isAdmin = roles.Any(r => r.IsBuiltIn);

        List<ModuleConfigEntity> modules;

        if (isAdmin)
        {
            // Admin user: return all enabled modules
            modules = await moduleRepository.GetListAsync(m => m.Enabled);
        }
        else
        {
            // Regular user: get modules based on role associations
            var roleModules = await roleModuleRepository.GetListAsync(rm => roleIds.Contains(rm.RoleId));
            var moduleIds = roleModules.Select(rm => rm.ModuleId).Distinct().ToList();

            if (!moduleIds.Any())
            {
                return new List<ModuleConfigDto>();
            }

            modules = await moduleRepository.GetListAsync(m => moduleIds.Contains(m.Id) && m.Enabled);
        }

        if (!modules.Any())
        {
            return new List<ModuleConfigDto>();
        }

        // 4. Sort by Order property
        var sortedModules = modules.OrderBy(m => m.Order).ToList();

        // 5. Map entities to DTOs
        return ObjectMapper.Map<List<ModuleConfigEntity>, List<ModuleConfigDto>>(sortedModules);
    }

    /// <summary>
    /// Create a new module configuration
    /// Requirements: 1.1, 1.3, 1.5, 10.1
    /// </summary>
    [Permission(SystemPermissions.ModuleConfig.Create)]
    [SqlSugarUnitOfWork]
    public override async Task<ModuleConfigDto> CreateAsync(CreateModuleConfigDto input)
    {
        // 1. Validate name uniqueness
        if (await moduleRepository.CheckNameExistsAsync(input.Name))
        {
            throw new BusinessException(SystemErrorCodes.ModuleNameAlreadyExists, "A module with the same name already exists.")
                .WithData("Name", input.Name);
        }

        // 2. Create entity
        var entity = new ModuleConfigEntity
        {
            Name = input.Name,
            Description = input.Description,
            Version = input.Version,
            Enabled = input.Enabled,
            Icon = input.Icon,
            Order = input.Order,
            Layout = input.Layout,
            EmptyRedirect = input.EmptyRedirect
        };

        // 3. Save to database
        await moduleRepository.InsertAsync(entity);

        if (input.RouteIds is not null)
        {
            await SyncModuleRoutesAsync(entity.Id, input.RouteIds);
        }

        // 4. Return DTO
        var dto = ObjectMapper.Map<ModuleConfigEntity, ModuleConfigDto>(entity);
        await FillRouteIdsAsync([dto]);
        return dto;
    }

    /// <summary>
    /// 按 DTO 主键更新模块配置。
    /// </summary>
    public override Task<ModuleConfigDto> UpdateAsync(UpdateModuleConfigDto input) => base.UpdateAsync(input);

    /// <summary>
    /// 更新指定模块配置及其路由关联。
    /// Requirements: 2.1, 2.2, 2.3, 2.5, 10.2
    /// </summary>
    [Permission(SystemPermissions.ModuleConfig.Update)]
    [SqlSugarUnitOfWork]
    public async Task<ModuleConfigDto> UpdateAsync(Guid id, UpdateModuleConfigDto input)
    {
        // 1. Get existing entity
        var entity = await moduleRepository.GetAsync(id);
        if (entity == null)
        {
            throw new ModuleNotFoundException(id);
        }

        // 2. Validate name uniqueness (excluding self)
        if (await moduleRepository.CheckNameExistsAsync(input.Name, id))
        {
            throw new BusinessException(SystemErrorCodes.ModuleNameAlreadyExists, "A module with the same name already exists.")
                .WithData("Name", input.Name);
        }

        // 3. Update properties
        entity.Name = input.Name;
        entity.Description = input.Description;
        entity.Version = input.Version;
        entity.Enabled = input.Enabled;
        entity.Icon = input.Icon;
        entity.Order = input.Order;
        entity.Layout = input.Layout;
        entity.EmptyRedirect = input.EmptyRedirect;

        // 4. Save updates
        await moduleRepository.UpdateAsync(entity);

        if (input.RouteIds is not null)
        {
            await SyncModuleRoutesAsync(id, input.RouteIds);
        }

        // 5. Return DTO
        var dto = ObjectMapper.Map<ModuleConfigEntity, ModuleConfigDto>(entity);
        await FillRouteIdsAsync([dto]);
        return dto;
    }

    /// <summary>
    /// Delete a module configuration with cascade delete
    /// Requirements: 3.1, 3.2, 3.3, 10.3
    /// </summary>
    [Permission(SystemPermissions.ModuleConfig.Delete)]
    [SqlSugarUnitOfWork]
    public override async Task<bool> DeleteAsync(Guid id)
    {
        // 1. Verify module exists
        var entity = await moduleRepository.GetAsync(id);
        if (entity == null)
        {
            throw new ModuleNotFoundException(id);
        }

        // 2. Remove this module's route associations; routes remain in the global catalog
        var links = await moduleRouteRepository.GetListAsync(link => link.ModuleId == id);
        if (links.Count > 0)
        {
            await moduleRouteRepository.DeleteManyAsync(links);
        }

        // 3. Delete module
        await moduleRepository.DeleteAsync(entity.Id);
        return true;
    }

    /// <summary>
    /// Get a single module configuration by ID
    /// Requirements: 2.2, 3.2
    /// </summary>
    public override async Task<ModuleConfigDto> GetAsync(Guid id)
    {
        await EnsureCanReadCatalogAsync();

        var entity = await moduleRepository.GetAsync(id);
        if (entity == null)
        {
            throw new ModuleNotFoundException(id);
        }

        var dto = ObjectMapper.Map<ModuleConfigEntity, ModuleConfigDto>(entity);
        await FillRouteIdsAsync([dto]);
        return dto;
    }

    /// <summary>
    /// 分页获取模块配置列表。
    /// </summary>
    public override async Task<PagedResultDto<ModuleConfigDto>> GetListAsync(
        PagedAndSortedResultRequestDto input)
    {
        await EnsureCanReadCatalogAsync();

        var result = await base.GetListAsync(input);
        await FillRouteIdsAsync(result.Items);
        return result;
    }

    private async Task FillRouteIdsAsync(List<ModuleConfigDto> modules)
    {
        if (modules.Count == 0)
        {
            return;
        }

        var moduleIds = modules
            .Select(module => Guid.TryParse(module.Id, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (moduleIds.Count == 0)
        {
            return;
        }

        var links = await moduleRouteRepository.GetListAsync(
            link => moduleIds.Contains(link.ModuleId));
        var routeIdsByModule = links
            .GroupBy(link => link.ModuleId)
            .ToDictionary(group => group.Key, group => group.Select(link => link.RouteId).ToList());

        foreach (var module in modules)
        {
            if (Guid.TryParse(module.Id, out var id))
            {
                module.RouteIds = routeIdsByModule.GetValueOrDefault(id) ?? [];
            }
        }
    }

    /// <summary>
    /// 覆盖式同步模块关联路由，并自动补齐所选路由的祖先，保证模块内路由树路径完整。
    /// </summary>
    private async Task SyncModuleRoutesAsync(Guid moduleId, IReadOnlyCollection<Guid> routeIds)
    {
        var distinctRouteIds = routeIds.Distinct().ToList();
        if (distinctRouteIds.Count > 0)
        {
            var selectedRoutes = await routeRepository.GetListAsync(
                route => distinctRouteIds.Contains(route.Id));
            if (selectedRoutes.Count != distinctRouteIds.Count)
            {
                throw new BusinessException(
                    SystemErrorCodes.RouteNotFound,
                    "One or more routes do not exist.");
            }

            var allRoutes = await routeRepository.GetListAsync(route => true);
            var routeById = allRoutes.ToDictionary(route => route.Id);
            var withAncestors = new HashSet<Guid>();
            foreach (var route in selectedRoutes)
            {
                Guid? currentId = route.Id;
                while (currentId.HasValue && withAncestors.Add(currentId.Value))
                {
                    currentId = routeById.TryGetValue(currentId.Value, out var current)
                        ? current.ParentId
                        : null;
                }
            }

            distinctRouteIds = withAncestors.ToList();
        }

        var existingLinks = await moduleRouteRepository.GetListAsync(
            link => link.ModuleId == moduleId);
        if (existingLinks.Count > 0)
        {
            await moduleRouteRepository.DeleteManyAsync(existingLinks);
        }

        foreach (var routeId in distinctRouteIds)
        {
            await moduleRouteRepository.InsertAsync(new ModuleRouteEntity
            {
                ModuleId = moduleId,
                RouteId = routeId
            });
        }
    }

    /// <summary>
    /// 模块目录服务于模块管理与角色模块分配；普通登录用户只读取自己的模块列表。
    /// </summary>
    private async Task EnsureCanReadCatalogAsync()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new AuthorizationException();

        string[] allowedCodes =
        [
            SystemPermissions.ModuleConfig.Create,
            SystemPermissions.ModuleConfig.Update,
            SystemPermissions.ModuleConfig.Delete,
            SystemPermissions.RouteConfig.Create,
            SystemPermissions.RouteConfig.Update,
            SystemPermissions.RouteConfig.Delete,
            SystemPermissions.RoleModule.Manage
        ];

        foreach (var code in allowedCodes)
        {
            var result = await authorizationService.AuthorizeAsync(
                httpContext.User,
                null,
                new PermissionAttribute(code));
            if (result.Succeeded)
            {
                return;
            }
        }

        throw new ForbiddenException(
            SystemErrorCodes.ModuleCatalogAccessDenied,
            "Reading module configuration requires module or role-module management permission.");
    }
}
