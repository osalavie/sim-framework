using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.Worker;

/// <summary>
/// 监控服务接口：提供后台任务（Worker）运行信息的查询能力。
/// </summary>
public interface IMonitorService : IApplicationService
{
    /// <summary>
    /// 获取所有后台任务（Worker）的运行信息列表。
    /// </summary>
    /// <returns>后台任务运行信息列表</returns>
    public Task<List<WorkerInfo>> GetWorkerInfosAsync();
}