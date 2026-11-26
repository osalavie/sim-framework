using Sim.Framework.Domain.Shared.Dependency;
using Sim.System.Application.Contracts.E2ee;

namespace Sim.System.Application.E2ee;

/// <summary>
/// 平台基座没有私有文章资源，E2EE 身份重置无需等待外部内容模块确认。
/// </summary>
public sealed class NoopUserE2eeArticleGuard : IUserE2eeArticleGuard, ITransientDependency
{
    public Task<UserE2eeArticleSummary> GetSummaryAsync(Guid userId) =>
        Task.FromResult(new UserE2eeArticleSummary());
}
