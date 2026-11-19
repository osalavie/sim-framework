using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Services;
using Sim.System.Application.Contracts.SystemConfig;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.User;

namespace Sim.System.Application.SystemConfig;

/// <summary>
/// 系统配置服务
/// </summary>
public class SystemConfigService : ApplicationService, ISystemConfigService
{
    /// <summary>
    /// 获取系统配置（公开接口，无需认证）
    /// </summary>
    [AllowAnonymous]
    public Task<SystemConfigDto> GetAsync()
    {
        var config = new SystemConfigDto
        {
            Roles = new RoleConfigDto
            {
                AdminRole = RoleConst.Admin,
                UserRole = RoleConst.User
            }
        };

        return Task.FromResult(config);
    }
}
