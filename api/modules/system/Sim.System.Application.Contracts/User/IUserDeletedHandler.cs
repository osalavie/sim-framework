namespace Sim.System.Application.Contracts.User;

/// <summary>
/// 用户删除前的扩展清理端口。
/// 实现方在用户行删除前同步清理或匿名化自身模块持有的用户数据；
/// 异常会向上抛出，由删除流程整体回滚。
/// </summary>
public interface IUserDeletedHandler
{
    Task HandleAsync(UserDeletedContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// 被删除用户的最小标识，供扩展模块定位自身数据；不包含姓名、邮箱等个人资料。
/// </summary>
public sealed record UserDeletedContext(
    Guid UserId,
    Guid? TenantId);
