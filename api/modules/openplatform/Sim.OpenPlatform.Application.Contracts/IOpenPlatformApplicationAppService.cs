using Sim.Framework.Application.Contracts.IServices;
using Sim.OpenPlatform.Application.Contracts.Dtos;

namespace Sim.OpenPlatform.Application.Contracts;

/// <summary>
/// 开放平台应用管理 — 管理端维护第三方接入应用及其授权范围。
/// </summary>
public interface IOpenPlatformApplicationAppService : IApplicationService
{
    /// <summary>
    /// 获取全部第三方应用列表（按创建时间倒序）。
    /// </summary>
    /// <returns>第三方应用 DTO 列表。</returns>
    Task<List<OpenPlatformApplicationDto>> GetListAsync();

    /// <summary>
    /// 根据 ID 获取第三方应用详情。
    /// </summary>
    /// <param name="id">应用唯一标识。</param>
    /// <returns>第三方应用 DTO。</returns>
    Task<OpenPlatformApplicationDto> GetAsync(Guid id);

    /// <summary>
    /// 创建第三方应用，自动生成 AppId 与 AppSecret。
    /// </summary>
    /// <param name="input">应用创建数据（名称、描述、授权范围、允许的客户端 IP）。</param>
    /// <returns>创建结果，包含应用 ID、AppId 及明文 AppSecret（仅此一次返回）。</returns>
    Task<CreateOpenPlatformApplicationResultDto> CreateAsync(CreateOpenPlatformApplicationDto input);

    /// <summary>
    /// 更新第三方应用信息（名称、描述、授权范围、允许的客户端 IP）。
    /// </summary>
    /// <param name="id">应用唯一标识。</param>
    /// <param name="input">应用更新数据，仅更新非空字段。</param>
    /// <returns>更新后的第三方应用 DTO。</returns>
    Task<OpenPlatformApplicationDto> UpdateAsync(Guid id, UpdateOpenPlatformApplicationDto input);

    /// <summary>
    /// 启用第三方应用，启用后允许其通过令牌端点换取 Access Token。
    /// </summary>
    /// <param name="id">应用唯一标识。</param>
    Task EnableAsync(Guid id);

    /// <summary>
    /// 禁用第三方应用，禁用后令牌端点将拒绝其签发请求。
    /// </summary>
    /// <param name="id">应用唯一标识。</param>
    Task DisableAsync(Guid id);

    /// <summary>
    /// 删除第三方应用及其凭证配置。
    /// </summary>
    /// <param name="id">应用唯一标识。</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// 重置第三方应用的 AppSecret，旧密钥立即失效。
    /// </summary>
    /// <param name="id">应用唯一标识。</param>
    /// <returns>重置结果，包含明文 AppSecret（仅此一次返回）。</returns>
    Task<ResetOpenPlatformSecretResultDto> ResetSecretAsync(Guid id);
}