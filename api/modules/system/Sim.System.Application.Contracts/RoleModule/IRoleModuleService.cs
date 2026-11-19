using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.RoleModule;

/// <summary>
/// 角色-模块关联服务接口
/// </summary>
public interface IRoleModuleService : IApplicationService
{
    /// <summary>
    /// 为角色分配模块
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="moduleIds">模块ID列表</param>
    Task AssignModulesToRoleAsync(Guid roleId, List<Guid> moduleIds);

    /// <summary>
    /// 获取角色的模块列表
    /// </summary>
    /// <param name="roleId">角色ID</param>
    Task<List<Guid>> GetRoleModulesAsync(Guid roleId);

    /// <summary>
    /// 移除角色的模块
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="moduleId">模块ID</param>
    Task RemoveModuleFromRoleAsync(Guid roleId, Guid moduleId);

    /// <summary>
    /// 清空角色的所有模块
    /// </summary>
    /// <param name="roleId">角色ID</param>
    Task ClearRoleModulesAsync(Guid roleId);
}
