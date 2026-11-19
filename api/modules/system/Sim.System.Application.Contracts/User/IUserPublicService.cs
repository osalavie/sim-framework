using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.User;

/// <summary>
/// 跨模块只读用户信息查询服务（暴露头像等展示数据）。
/// </summary>
public interface IUserPublicService : IApplicationService
{
    /// <summary>
    /// 批量获取用户的头像 URL。返回的字典只包含提供方找到的用户；
    /// 未找到的用户不会出现在结果中，调用方按需回退。
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string?>> GetAvatarsAsync(IReadOnlyCollection<Guid> userIds);

    /// <summary>
    /// 批量获取用户显示名（昵称优先，昵称为空时回退账号名）与账号名，供创建人/修改人列展示与悬停气泡。
    /// 返回字典只包含找到的用户。
    /// </summary>
    Task<IReadOnlyDictionary<Guid, UserDisplayNameDto>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds);

    /// <summary>
    /// 检查昵称是否可用；excludeUserId 用于编辑场景排除当前账号。
    /// </summary>
    Task<bool> GetNickNameAvailabilityAsync(string? nickName, Guid? excludeUserId = null);
}
