using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.SystemConfig;

/// <summary>
/// 系统配置服务接口
/// </summary>
public interface ISystemConfigService : IApplicationService
{
    /// <summary>
    /// 获取系统配置（公开接口，无需认证）
    /// </summary>
    Task<SystemConfigDto> GetAsync();
}
