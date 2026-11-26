namespace Sim.Framework.Application.Contracts.IServices;

/// <summary>
/// 应用服务标记接口，替代 Volo.Abp.Application.Services.IApplicationService。
/// 用于依赖注入约定注册；实现该接口的应用服务会被自动暴露为 HTTP API。
/// </summary>
public interface IApplicationService;
