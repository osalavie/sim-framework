using SqlSugar;
using Sim.Framework.Domain.Shared.Dependency;
using Sim.System.Application.Contracts.User;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.ExternalLogin;
using Sim.System.Domain.FileManagement;
using Sim.System.Domain.Organization;
using Sim.System.Domain.Security;

namespace Sim.System.Application.User;

/// <summary>
/// 用户删除时清理系统模块内的账号级数据：外部登录绑定、会话、组织成员、存储配额与分享链接。
/// 组织单元只解除负责人引用，不删除组织本身；文件与审计记录作为公共资源/审计保留。
/// </summary>
public sealed class SystemUserAccountDataCleanupHandler(
    ISqlSugarClient database,
    IExternalLoginRepository externalLoginRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ISqlSugarRepository<OrganizationMembershipEntity> organizationMembershipRepository,
    ISqlSugarRepository<StorageQuotaEntity> storageQuotaRepository,
    ISqlSugarRepository<ResourceShareLinkEntity> shareLinkRepository,
    ISqlSugarRepository<ResourceShareLinkViewerEntity> shareLinkViewerRepository)
    : IUserDeletedHandler, ITransientDependency
{
    public async Task HandleAsync(UserDeletedContext context, CancellationToken cancellationToken = default)
    {
        var userId = context.UserId;

        await externalLoginRepository.DeleteAsync(row => row.UserId == userId);
        await refreshTokenRepository.DeleteAsync(token => token.UserId == userId);
        await organizationMembershipRepository.DeleteAsync(row => row.UserId == userId);
        await database.Updateable<OrganizationUnitEntity>()
            .SetColumns(unit => new OrganizationUnitEntity { ManagerUserId = null })
            .Where(unit => unit.ManagerUserId == userId)
            .ExecuteCommandAsync();
        await storageQuotaRepository.DeleteAsync(row => row.UserId == userId);

        var shareLinks = await shareLinkRepository.GetListAsync(link => link.OwnerId == userId);
        if (shareLinks.Count == 0)
        {
            return;
        }

        var shareLinkIds = shareLinks.Select(link => link.Id).ToList();
        await shareLinkViewerRepository.DeleteAsync(viewer => shareLinkIds.Contains(viewer.ShareLinkId));
        await shareLinkRepository.DeleteManyAsync(shareLinks);
    }
}
