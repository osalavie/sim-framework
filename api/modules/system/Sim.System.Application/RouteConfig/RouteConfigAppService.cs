using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Domain.Shared.Security;
using Sim.System.Application.Contracts.Dtos;
using Sim.System.Application.Contracts.RouteConfig;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.System.Domain.Shared.Consts;
using Sim.Framework.Core.UnitOfWork;
using Sim.System.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.ModuleConfig;
using Sim.System.Domain.Permission;
using Sim.System.Domain.Role;
using Sim.System.Domain.RouteConfig;
using Sim.System.Domain.RouteMeta;
using Sim.System.Domain.User;

namespace Sim.System.Application.RouteConfig;

/// <summary>
/// Route configuration application service implementation
/// </summary>
public class RouteConfigAppService(
        IRouteConfigRepository routeRepository,
        IRouteMetaRepository metaRepository,
        IModuleConfigRepository moduleRepository,
        IUserRoleRepository userRoleRepository,
        IRoleModuleRepository roleModuleRepository,
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IModuleRouteRepository moduleRouteRepository,
        ICurrentUser currentUser,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor) : CrudAppService<Guid, RouteConfigEntity, RouteConfigDto, CreateRouteConfigDto, UpdateRouteConfigDto>(routeRepository), IRouteConfigAppService
{
    /// <summary>
    /// Get paginated list of routes with metadata.
    /// Routes are a global catalog; module membership resolves through module-route associations.
    /// </summary>
    [Authorize]
    public override async Task<PagedResultDto<RouteConfigDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        await EnsureCanReadCatalogAsync();

        if (!currentUser.IsAuthenticated || !currentUser.Id.HasValue)
        {
            return new PagedResultDto<RouteConfigDto>(0, new List<RouteConfigDto>());
        }

        var allRoutes = await LoadRoutesWithMetaAsync();
        if (allRoutes.Count == 0)
        {
            return new PagedResultDto<RouteConfigDto>(0, new List<RouteConfigDto>());
        }

        if (!string.IsNullOrWhiteSpace(input.Sorting))
        {
            allRoutes = ApplySorting(allRoutes, input.Sorting);
        }
        else
        {
            allRoutes = allRoutes
                .OrderBy(r => r.Meta?.Order ?? 0)
                .ThenBy(r => r.Name)
                .ToList();
        }

        var totalCount = allRoutes.Count;
        var pagedRoutes = allRoutes
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var routeDtos = ObjectMapper.Map<List<RouteConfigEntity>, List<RouteConfigDto>>(pagedRoutes);
        await ApplyModuleInfoAsync(routeDtos, allRoutes.Select(route => route.Id).ToList());

        return new PagedResultDto<RouteConfigDto>(totalCount, routeDtos);
    }

    /// <summary>
    /// Load every route definition together with its metadata.
    /// </summary>
    private async Task<List<RouteConfigEntity>> LoadRoutesWithMetaAsync()
    {
        var routes = await routeRepository.GetListAsync(route => true);
        if (routes.Count == 0)
        {
            return routes;
        }

        var routeIds = routes.Select(route => route.Id).ToList();
        var metas = await metaRepository.GetByRouteConfigIdsAsync(routeIds);
        var metaByRouteId = metas.ToDictionary(meta => meta.RouteConfigId);
        foreach (var route in routes)
        {
            route.Meta = metaByRouteId.GetValueOrDefault(route.Id);
        }

        return routes;
    }

    /// <summary>
    /// Resolve each route's module memberships from the association table.
    /// </summary>
    private async Task ApplyModuleInfoAsync(List<RouteConfigDto> routes, IReadOnlyCollection<Guid> routeIds)
    {
        if (routes.Count == 0 || routeIds.Count == 0)
        {
            return;
        }

        var links = await moduleRouteRepository.GetListAsync(link => routeIds.Contains(link.RouteId));
        var moduleIdsByRoute = links
            .GroupBy(link => link.RouteId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.ModuleId).Distinct().ToList());

        var moduleNameById = new Dictionary<Guid, string>();
        var moduleIds = links.Select(link => link.ModuleId).Distinct().ToList();
        if (moduleIds.Count > 0)
        {
            var modules = await moduleRepository.GetListAsync(module => moduleIds.Contains(module.Id));
            moduleNameById = modules.ToDictionary(module => module.Id, module => module.Name);
        }

        ApplyModuleInfo(routes, moduleIdsByRoute, moduleNameById);
    }

    private static void ApplyModuleInfo(
        List<RouteConfigDto> routes,
        IReadOnlyDictionary<Guid, List<Guid>> moduleIdsByRoute,
        IReadOnlyDictionary<Guid, string> moduleNameById)
    {
        foreach (var route in routes)
        {
            route.ModuleIds = moduleIdsByRoute.GetValueOrDefault(route.Id) ?? [];
            route.ModuleName = string.Join(
                ", ",
                route.ModuleIds
                    .Select(moduleId => moduleNameById.GetValueOrDefault(moduleId))
                    .Where(name => !string.IsNullOrWhiteSpace(name)));

            if (route.Children is { Count: > 0 })
            {
                ApplyModuleInfo(route.Children, moduleIdsByRoute, moduleNameById);
            }
        }
    }

    /// <summary>
    /// Apply sorting to route list
    /// </summary>
    private List<RouteConfigEntity> ApplySorting(List<RouteConfigEntity> routes, string sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return routes;
        }

        // Parse sorting string (format: "field1 asc, field2 desc")
        var sortParts = sorting.Split(',', StringSplitOptions.RemoveEmptyEntries);
        IOrderedEnumerable<RouteConfigEntity>? orderedRoutes = null;

        foreach (var sortPart in sortParts)
        {
            var parts = sortPart.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var field = parts[0].ToLower();
            var isDescending = parts.Length > 1 && parts[1].ToLower() == "desc";

            if (orderedRoutes == null)
            {
                orderedRoutes = ApplySortField(routes.AsEnumerable(), field, isDescending, true);
            }
            else
            {
                orderedRoutes = ApplySortField(orderedRoutes, field, isDescending, false);
            }
        }

        return orderedRoutes?.ToList() ?? routes;
    }

    /// <summary>
    /// Apply sorting for a specific field
    /// </summary>
    private IOrderedEnumerable<RouteConfigEntity> ApplySortField(
        IEnumerable<RouteConfigEntity> routes, 
        string field, 
        bool isDescending, 
        bool isFirst)
    {
        Func<RouteConfigEntity, object> keySelector = field switch
        {
            "name" => r => r.Name,
            "path" => r => r.Path,
            "order" => r => r.Meta?.Order ?? 0,
            "title" => r => r.Meta?.Title ?? string.Empty,
            _ => r => r.Name
        };

        if (isFirst)
        {
            return isDescending 
                ? routes.OrderByDescending(keySelector) 
                : routes.OrderBy(keySelector);
        }
        else
        {
            var orderedRoutes = (IOrderedEnumerable<RouteConfigEntity>)routes;
            return isDescending 
                ? orderedRoutes.ThenByDescending(keySelector) 
                : orderedRoutes.ThenBy(keySelector);
        }
    }

    /// <summary>
    /// Get routes for a specific module based on user's roles
    /// If user has Admin role, return all routes for the module
    /// </summary>
    [Authorize]
    public async Task<List<RouteConfigDto>> GetModuleRoutesAsync(Guid moduleId)
    {
        // 1. Validate module ID exists
        var module = await moduleRepository.GetAsync(moduleId);
        if (module == null)
        {
            throw new ModuleNotFoundException(moduleId);
        }

        // 2. Check if user is authenticated
        if (!currentUser.IsAuthenticated || !currentUser.Id.HasValue)
        {
            return new List<RouteConfigDto>();
        }

        var userId = currentUser.Id.Value;

        // 3. Get user's roles
        var userRoles = await userRoleRepository.GetListAsync(ur => ur.UserId == userId);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();

        if (!roleIds.Any())
        {
            throw new ModuleAccessDeniedException(moduleId, module.Name);
        }

        // 4. Check if user has Admin role
        var roles = await roleRepository.GetListAsync(r => roleIds.Contains(r.Id));
        var isAdmin = roles.Any(r => r.IsBuiltIn);

        if (!isAdmin)
        {
            // Regular user: check if they have access to this module through role associations
            var roleModules = await roleModuleRepository.GetListAsync(rm => roleIds.Contains(rm.RoleId));
            var accessibleModuleIds = roleModules.Select(rm => rm.ModuleId).Distinct().ToList();
            
            if (!accessibleModuleIds.Contains(moduleId))
            {
                throw new ModuleAccessDeniedException(moduleId, module.Name);
            }
        }
        // Admin user: skip module access check, they have access to all modules

        // 5. Get routes associated with this module
        var allRoutes = await GetModuleRoutesWithMetaByAssociationAsync(moduleId);
        
        if (allRoutes == null || !allRoutes.Any())
        {
            return new List<RouteConfigDto>();
        }

        // 6. Build route tree and remove routes the user cannot access
        var routeTree = FilterRouteTreeByPermission(
            BuildRouteTree(allRoutes),
            GetCurrentPermissionCodes(),
            isAdmin);

        // 7. Map entities to DTOs
        return ObjectMapper.Map<List<RouteConfigEntity>, List<RouteConfigDto>>(routeTree);
    }

    /// <summary>
    /// 路由权限码必须来自权限清单，避免手填错别字导致页面无人可见。
    /// </summary>
    private async Task EnsurePermissionCodesExistAsync(string? permissionCode)
    {
        var codes = (permissionCode ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (codes.Count == 0)
        {
            return;
        }

        var known = await permissionRepository.GetListAsync(permission => codes.Contains(permission.Code));
        var knownCodes = known.Select(permission => permission.Code).ToHashSet(StringComparer.Ordinal);
        var missing = codes.Where(code => !knownCodes.Contains(code)).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessException(
                    SystemErrorCodes.RoutePermissionCodeNotFound,
                    "One or more route permission codes do not exist.")
                .WithData("Codes", string.Join(",", missing));
        }
    }

    /// <summary>
    /// 当前请求携带的权限码集合（来自 JWT Claim）。
    /// </summary>
    private HashSet<string> GetCurrentPermissionCodes() =>
        currentUser.FindClaims(TokenTypeConst.Permission)
            .Select(claim => claim.Value)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 按模块-路由关联读取模块下发的路由。
    /// </summary>
    private async Task<List<RouteConfigEntity>> GetModuleRoutesWithMetaByAssociationAsync(Guid moduleId)
    {
        var links = await moduleRouteRepository.GetListAsync(link => link.ModuleId == moduleId);
        var routeIds = links.Select(link => link.RouteId).Distinct().ToList();
        if (routeIds.Count == 0)
        {
            return [];
        }

        var routes = await routeRepository.GetListAsync(route => routeIds.Contains(route.Id));
        if (routes.Count == 0)
        {
            return [];
        }

        var metas = await metaRepository.GetByRouteConfigIdsAsync(routeIds);
        foreach (var route in routes)
        {
            route.Meta = metas.FirstOrDefault(meta => meta.RouteConfigId == route.Id);
        }

        return routes;
    }

    /// <summary>
    /// 路由的 PermissionCode 为空表示不限制；多个权限码按 PermissionRequireAll 决定任一或全部满足。
    /// </summary>
    private static bool CanAccessRoute(
        RouteConfigEntity route,
        HashSet<string> permissions,
        bool isAdmin)
    {
        if (isAdmin)
        {
            return true;
        }

        return RoutePermissionPolicy.CanAccess(
            route.Meta?.PermissionCode,
            permissions,
            route.Meta?.PermissionRequireAll ?? false);
    }

    /// <summary>
    /// 无权限的父路由会连同其子树一起被移除。
    /// </summary>
    private static List<RouteConfigEntity> FilterRouteTreeByPermission(
        List<RouteConfigEntity> routes,
        HashSet<string> permissions,
        bool isAdmin)
    {
        var result = new List<RouteConfigEntity>(routes.Count);
        foreach (var route in routes)
        {
            if (!CanAccessRoute(route, permissions, isAdmin))
            {
                continue;
            }

            if (route.Children is { Count: > 0 })
            {
                route.Children = FilterRouteTreeByPermission(route.Children, permissions, isAdmin);
            }

            result.Add(route);
        }

        return result;
    }

    /// <summary>
    /// Build hierarchical route tree from flat list
    /// </summary>
    private List<RouteConfigEntity> BuildRouteTree(List<RouteConfigEntity> routes)
    {
        // Create a dictionary for quick lookup
        var routeDict = routes.ToDictionary(r => r.Id);

        // Find root routes (routes without parent or parent not in accessible list)
        var rootRoutes = new List<RouteConfigEntity>();

        foreach (var route in routes)
        {
            if (route.ParentId == null || !routeDict.ContainsKey(route.ParentId.Value))
            {
                // This is a root route
                rootRoutes.Add(route);
            }
            else
            {
                // This is a child route, add it to parent's children
                var parent = routeDict[route.ParentId.Value];
                parent.Children ??= new List<RouteConfigEntity>();
                parent.Children.Add(route);
            }
        }

        // Recursively sort children by order in meta
        SortRoutesByOrder(rootRoutes);

        return rootRoutes;
    }

    /// <summary>
    /// Recursively sort routes by order property in meta
    /// </summary>
    private void SortRoutesByOrder(List<RouteConfigEntity> routes)
    {
        if (routes == null || !routes.Any())
        {
            return;
        }

        // Sort current level by order from meta
        routes.Sort((a, b) =>
        {
            var orderA = a.Meta?.Order ?? 0;
            var orderB = b.Meta?.Order ?? 0;
            return orderA.CompareTo(orderB);
        });

        // Recursively sort children
        foreach (var route in routes)
        {
            if (route.Children != null && route.Children.Any())
            {
                SortRoutesByOrder(route.Children);
            }
        }
    }

    /// <summary>
    /// Create a new route configuration
    /// Requirements: 4.1, 4.3, 4.4, 4.5, 4.6, 4.7, 10.1, 12.4
    /// </summary>
    [Permission(SystemPermissions.RouteConfig.Create)]
    [SqlSugarUnitOfWork]
    public override async Task<RouteConfigDto> CreateAsync(CreateRouteConfigDto input)
    {
        // 1. Validate parent route exists (if provided)
        if (input.ParentId.HasValue)
        {
            var parent = await routeRepository.GetAsync(input.ParentId.Value);
            if (parent == null)
            {
            throw new BusinessException(SystemErrorCodes.ParentRouteNotFound, "The parent route does not exist.")
                    .WithData("ParentId", input.ParentId.Value);
            }
        }

        // 2. Validate route name uniqueness (route names are globally unique)
        if (await routeRepository.CheckNameExistsAsync(input.Name))
        {
            throw new BusinessException(SystemErrorCodes.RouteNameAlreadyExists, "A route with the same name already exists.")
                .WithData("Name", input.Name);
        }

        await EnsurePermissionCodesExistAsync(input.Meta?.PermissionCode);

        // 3. Create route entity
        var routeEntity = new RouteConfigEntity
        {
            ParentId = input.ParentId,
            Name = input.Name,
            Path = input.Path,
            Component = input.Component,
            Redirect = input.Redirect
        };

        await routeRepository.InsertAsync(routeEntity);

        // 4. Create metadata (if provided)
        RouteMetaEntity? metaEntity = null;
        if (input.Meta != null)
        {
            metaEntity = new RouteMetaEntity
            {
                RouteConfigId = routeEntity.Id,
                Title = input.Meta.Title,
                Icon = input.Meta.Icon,
                Menu = input.Meta.Menu,
                Tab = input.Meta.Tab,
                KeepAlive = input.Meta.KeepAlive,
                Pinned = input.Meta.Pinned,
                Anonymous = input.Meta.Anonymous,
                Disabled = input.Meta.Disabled,
                Order = input.Meta.Order,
                Module = input.Meta.Module,
                PermissionCode = input.Meta.PermissionCode,
                PermissionRequireAll = input.Meta.PermissionRequireAll
            };

            await metaRepository.InsertAsync(metaEntity);
            routeEntity.Meta = metaEntity;
        }

        // 5. Return DTO
        return ObjectMapper.Map<RouteConfigEntity, RouteConfigDto>(routeEntity);
    }

    /// <summary>
    /// Update an existing route configuration
    /// Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 10.2, 12.4
    /// </summary>
    [Permission(SystemPermissions.RouteConfig.Update)]
    [SqlSugarUnitOfWork]
    public override async Task<RouteConfigDto> UpdateAsync(UpdateRouteConfigDto input)
    {
        var id = input.Id;
        
        // 1. Get existing route entity
        var entity = await routeRepository.GetAsync(id);
        if (entity == null)
        {
            throw new RouteNotFoundException(id);
        }

        // 2. Validate parent route (if changed)
        if (input.ParentId.HasValue)
        {
            // Check circular reference
            if (await routeRepository.CheckCircularReferenceAsync(id, input.ParentId.Value))
            {
            throw new BusinessException(SystemErrorCodes.CircularReferenceDetected, "Setting the parent route would create a circular reference.");
            }

            var parent = await routeRepository.GetAsync(input.ParentId.Value);
            if (parent == null)
            {
            throw new BusinessException(SystemErrorCodes.ParentRouteNotFound, "The parent route does not exist.")
                    .WithData("ParentId", input.ParentId.Value);
            }
        }

        // 3. Validate route name uniqueness (excluding self)
        if (await routeRepository.CheckNameExistsAsync(input.Name, id))
        {
            throw new BusinessException(SystemErrorCodes.RouteNameAlreadyExists, "A route with the same name already exists.")
                .WithData("Name", input.Name);
        }

        await EnsurePermissionCodesExistAsync(input.Meta?.PermissionCode);

        // 4. Update route properties
        entity.ParentId = input.ParentId;
        entity.Name = input.Name;
        entity.Path = input.Path;
        entity.Component = input.Component;
        entity.Redirect = input.Redirect;

        await routeRepository.UpdateAsync(entity);

        // 5. Update or create metadata
        if (input.Meta != null)
        {
            var metaEntity = await metaRepository.GetByRouteConfigIdAsync(id);

            if (metaEntity == null)
            {
                // Create new metadata
                metaEntity = new RouteMetaEntity
                {
                    RouteConfigId = id,
                    Title = input.Meta.Title,
                    Icon = input.Meta.Icon,
                    Menu = input.Meta.Menu,
                    Tab = input.Meta.Tab,
                    KeepAlive = input.Meta.KeepAlive,
                    Pinned = input.Meta.Pinned,
                    Anonymous = input.Meta.Anonymous,
                    Disabled = input.Meta.Disabled,
                    Order = input.Meta.Order,
                    Module = input.Meta.Module,
                    PermissionCode = input.Meta.PermissionCode,
                    PermissionRequireAll = input.Meta.PermissionRequireAll
                };
                
                await metaRepository.InsertAsync(metaEntity);
            }
            else
            {
                // Update metadata properties
                metaEntity.Title = input.Meta.Title;
                metaEntity.Icon = input.Meta.Icon;
                metaEntity.Menu = input.Meta.Menu;
                metaEntity.Tab = input.Meta.Tab;
                metaEntity.KeepAlive = input.Meta.KeepAlive;
                metaEntity.Pinned = input.Meta.Pinned;
                metaEntity.Anonymous = input.Meta.Anonymous;
                metaEntity.Disabled = input.Meta.Disabled;
                metaEntity.Order = input.Meta.Order;
                metaEntity.Module = input.Meta.Module;
                metaEntity.PermissionCode = input.Meta.PermissionCode;
                metaEntity.PermissionRequireAll = input.Meta.PermissionRequireAll;
                
                await metaRepository.UpdateAsync(metaEntity);
            }

            entity.Meta = metaEntity;
        }

        // 6. Return DTO
        var dto = ObjectMapper.Map<RouteConfigEntity, RouteConfigDto>(entity);
        await ApplyModuleInfoAsync([dto], [entity.Id]);
        return dto;
    }

    /// <summary>
    /// Delete a route configuration with cascade delete
    /// Requirements: 6.1, 6.2, 6.3, 6.4, 10.3
    /// </summary>
    [Permission(SystemPermissions.RouteConfig.Delete)]
    [SqlSugarUnitOfWork]
    public override async Task<bool> DeleteAsync(Guid id)
    {
        // 1. Verify route exists
        var entity = await routeRepository.GetAsync(id);
        if (entity == null)
        {
            throw new RouteNotFoundException(id);
        }

        // 2. Get all descendant routes (recursive)
        var descendants = await routeRepository.GetDescendantsAsync(id);

        // 3. Collect all route IDs (descendants + current)
        var allRouteIds = descendants.Select(r => r.Id).Append(id).ToList();

        // 4. Remove module-route associations for all deleted routes
        await moduleRouteRepository.DeleteAsync(link => allRouteIds.Contains(link.RouteId));

        // 5. Delete all metadata for these routes
        var metas = await metaRepository.GetByRouteConfigIdsAsync(allRouteIds);
        foreach (var meta in metas)
        {
            await metaRepository.DeleteAsync(meta.Id);
        }

        // 6. Delete all descendant routes
        foreach (var descendant in descendants)
        {
            await routeRepository.DeleteAsync(descendant.Id);
        }

        // 7. Delete current route
        await routeRepository.DeleteAsync(entity.Id);
        
        return true;
    }

    /// <summary>
    /// Get a single route configuration by ID
    /// Requirements: 5.2, 6.2
    /// </summary>
    [Authorize]
    public override async Task<RouteConfigDto> GetAsync(Guid id)
    {
        await EnsureCanReadCatalogAsync();

        var entity = await routeRepository.GetAsync(id);
        if (entity == null)
        {
            throw new RouteNotFoundException(id);
        }

        // Load associated metadata
        entity.Meta = await metaRepository.GetByRouteConfigIdAsync(id);

        return ObjectMapper.Map<RouteConfigEntity, RouteConfigDto>(entity);
    }

    /// <summary>
    /// Get the current user's accessible modules and one global route tree.
    /// Routes are assembled from the global catalog so a route whose parent
    /// belongs to another module stays under that parent instead of becoming a root.
    /// </summary>
    [Authorize]
    public async Task<UserMenuDto> GetUserAccessibleRoutesAsync()
    {
        var result = new UserMenuDto();

        // 1. Check if user is authenticated
        if (!currentUser.IsAuthenticated || !currentUser.Id.HasValue)
        {
            return result;
        }

        var userId = currentUser.Id.Value;

        // 2. Get user's roles
        var userRoles = await userRoleRepository.GetListAsync(ur => ur.UserId == userId);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();

        if (roleIds.Count == 0)
        {
            return result;
        }

        // 3. Check if user has Admin role
        var roles = await roleRepository.GetListAsync(r => roleIds.Contains(r.Id));
        var isAdmin = roles.Any(r => r.IsBuiltIn);

        // 4. Get accessible enabled modules
        List<ModuleConfigEntity> accessibleModules;

        if (isAdmin)
        {
            accessibleModules = await moduleRepository.GetListAsync(m => m.Enabled);
        }
        else
        {
            var roleModules = await roleModuleRepository.GetListAsync(rm => roleIds.Contains(rm.RoleId));
            var accessibleModuleIds = roleModules.Select(rm => rm.ModuleId).Distinct().ToList();
            accessibleModules = accessibleModuleIds.Count == 0
                ? []
                : await moduleRepository.GetListAsync(m =>
                    accessibleModuleIds.Contains(m.Id) && m.Enabled);
        }

        accessibleModules = accessibleModules
            .OrderBy(module => module.Order)
            .ThenBy(module => module.Name, StringComparer.Ordinal)
            .ToList();
        result.Modules = ObjectMapper.Map<List<ModuleConfigEntity>, List<ModuleConfigDto>>(accessibleModules);

        if (accessibleModules.Count == 0)
        {
            return result;
        }

        var moduleIdSet = accessibleModules.Select(module => module.Id).ToHashSet();
        var links = await moduleRouteRepository.GetListAsync(link => moduleIdSet.Contains(link.ModuleId));
        var accessibleRouteIds = links.Select(link => link.RouteId).ToHashSet();

        if (accessibleRouteIds.Count == 0)
        {
            return result;
        }

        // 5. Build one tree from the global catalog. Routes may be re-parented across
        // modules, so ancestors are added even when they are not associated with an
        // accessible module; otherwise the child would be treated as a root route.
        var allRoutes = await LoadRoutesWithMetaAsync();
        var routeById = allRoutes.ToDictionary(route => route.Id);
        var includedRouteIds = new HashSet<Guid>(accessibleRouteIds);

        foreach (var routeId in accessibleRouteIds)
        {
            if (!routeById.TryGetValue(routeId, out var route))
            {
                continue;
            }

            var parentId = route.ParentId;
            while (parentId.HasValue
                   && routeById.TryGetValue(parentId.Value, out var parent)
                   && includedRouteIds.Add(parent.Id))
            {
                parentId = parent.ParentId;
            }
        }

        var routes = allRoutes
            .Where(route => includedRouteIds.Contains(route.Id))
            .ToList();
        var routeTree = FilterRouteTreeByPermission(
            BuildRouteTree(routes),
            GetCurrentPermissionCodes(),
            isAdmin);

        // 6. Map to DTOs and attach module memberships for frontend layout resolution
        var routeDtos = ObjectMapper.Map<List<RouteConfigEntity>, List<RouteConfigDto>>(routeTree);
        await ApplyModuleInfoAsync(routeDtos, routes.Select(route => route.Id).ToList());
        result.Routes = routeDtos;

        return result;
    }

    /// <summary>
    /// Get paginated routes with metadata (management view).
    /// Returns the global route catalog; when a module is specified, only routes associated
    /// with that module are returned. Pagination only applies to root routes.
    /// </summary>
    [Authorize]
    public async Task<PagedResultDto<RouteConfigDto>> GetRoutesWithMetaPagedAsync(RouteConfigPagedRequestDto input)
    {
        await EnsureCanReadCatalogAsync();

        if (!currentUser.IsAuthenticated || !currentUser.Id.HasValue)
        {
            return new PagedResultDto<RouteConfigDto>(0, new List<RouteConfigDto>());
        }

        var allRoutes = await LoadRoutesWithMetaAsync();

        // 1. Apply module filter through the association table
        if (input.ModuleId.HasValue)
        {
            var moduleRouteIds = (await moduleRouteRepository.GetListAsync(
                    link => link.ModuleId == input.ModuleId.Value))
                .Select(link => link.RouteId)
                .ToHashSet();
            allRoutes = allRoutes.Where(route => moduleRouteIds.Contains(route.Id)).ToList();
        }

        // 2. Apply keyword filter if specified (matches name, path or menu title)
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            allRoutes = FilterRoutesByKeyword(allRoutes, input.Keyword.Trim());
        }

        if (allRoutes.Count == 0)
        {
            return new PagedResultDto<RouteConfigDto>(0, new List<RouteConfigDto>());
        }

        // 3. Separate root routes and child routes
        var rootRoutes = allRoutes.Where(r => r.ParentId == null).ToList();
        var childRoutes = allRoutes.Where(r => r.ParentId != null).ToList();

        // 4. Apply sorting to root routes
        if (!string.IsNullOrWhiteSpace(input.Sorting))
        {
            rootRoutes = ApplySorting(rootRoutes, input.Sorting);
        }
        else
        {
            rootRoutes = rootRoutes.OrderBy(r => r.Meta?.Order ?? 0)
                                   .ThenBy(r => r.Name)
                                   .ToList();
        }

        var totalCount = rootRoutes.Count;

        // 5. Apply pagination to root routes only and build the tree
        var pagedRootRoutes = rootRoutes
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();
        var routeTree = BuildTreeForRoots(pagedRootRoutes, childRoutes);

        // 6. Map entities to DTOs and resolve module memberships
        var routeDtos = ObjectMapper.Map<List<RouteConfigEntity>, List<RouteConfigDto>>(routeTree);
        await ApplyModuleInfoAsync(routeDtos, allRoutes.Select(route => route.Id).ToList());

        return new PagedResultDto<RouteConfigDto>(totalCount, routeDtos);
    }

    /// <summary>
    /// Filter routes by keyword, keeping the ancestor chain of matched routes so the tree has no dangling children
    /// </summary>
    private static List<RouteConfigEntity> FilterRoutesByKeyword(List<RouteConfigEntity> routes, string keyword)
    {
        var matchedIds = routes
            .Where(r => MatchesKeyword(r, keyword))
            .Select(r => r.Id)
            .ToHashSet();

        if (matchedIds.Count == 0)
        {
            return [];
        }

        var parentById = routes
            .Where(r => r.ParentId.HasValue)
            .ToDictionary(r => r.Id, r => r.ParentId!.Value);

        var includedIds = new HashSet<Guid>(matchedIds);
        foreach (var id in matchedIds)
        {
            var current = id;
            while (parentById.TryGetValue(current, out var parentId) && includedIds.Add(parentId))
            {
                current = parentId;
            }
        }

        return routes.Where(r => includedIds.Contains(r.Id)).ToList();
    }

    /// <summary>
    /// Check whether a route matches the keyword on name, path or menu title (case-insensitive)
    /// </summary>
    private static bool MatchesKeyword(RouteConfigEntity route, string keyword)
    {
        if (route.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        if (route.Path.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrEmpty(route.Meta?.Title) && route.Meta.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Build tree structure for specific root routes and their descendants
    /// </summary>
    private List<RouteConfigEntity> BuildTreeForRoots(List<RouteConfigEntity> rootRoutes, List<RouteConfigEntity> allChildRoutes)
    {
        // Create a dictionary for quick lookup
        var childDict = allChildRoutes.ToDictionary(r => r.Id);
        
        // Recursively attach children to their parents
        foreach (var root in rootRoutes)
        {
            AttachChildren(root, allChildRoutes, childDict);
        }

        // Sort children recursively
        SortRoutesByOrder(rootRoutes);

        return rootRoutes;
    }

    /// <summary>
    /// Recursively attach children to a parent route
    /// </summary>
    private void AttachChildren(RouteConfigEntity parent, List<RouteConfigEntity> allChildRoutes, Dictionary<Guid, RouteConfigEntity> childDict)
    {
        var children = allChildRoutes.Where(r => r.ParentId == parent.Id).ToList();
        
        if (children.Any())
        {
            parent.Children = children;
            
            // Recursively attach grandchildren
            foreach (var child in children)
            {
                AttachChildren(child, allChildRoutes, childDict);
            }
        }
    }

    /// <summary>
    /// Get the complete route catalog with metadata (management view, non-paginated).
    /// Route access filtering for menu delivery happens in the module-scoped endpoints.
    /// </summary>
    [Authorize]
    public async Task<List<RouteConfigDto>> GetRoutesWithMetaAsync()
    {
        await EnsureCanReadCatalogAsync();

        if (!currentUser.IsAuthenticated || !currentUser.Id.HasValue)
        {
            return new List<RouteConfigDto>();
        }

        var allRoutes = await LoadRoutesWithMetaAsync();
        if (allRoutes.Count == 0)
        {
            return new List<RouteConfigDto>();
        }

        var routeTree = BuildRouteTree(allRoutes);
        var routeDtos = ObjectMapper.Map<List<RouteConfigEntity>, List<RouteConfigDto>>(routeTree);
        await ApplyModuleInfoAsync(routeDtos, allRoutes.Select(route => route.Id).ToList());

        return routeDtos;
    }

    /// <summary>
    /// 路由/模块目录同时服务于路由管理、模块管理和角色模块分配页，
    /// 因此任一相关管理权限即可读取；普通登录用户只能读取自己的可访问路由。
    /// </summary>
    private async Task EnsureCanReadCatalogAsync()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new AuthorizationException();

        string[] allowedCodes =
        [
            SystemPermissions.RouteConfig.Create,
            SystemPermissions.RouteConfig.Update,
            SystemPermissions.RouteConfig.Delete,
            SystemPermissions.ModuleConfig.Create,
            SystemPermissions.ModuleConfig.Update,
            SystemPermissions.ModuleConfig.Delete,
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
            SystemErrorCodes.RouteCatalogAccessDenied,
            "Reading route configuration requires route or role-module management permission.");
    }
}
