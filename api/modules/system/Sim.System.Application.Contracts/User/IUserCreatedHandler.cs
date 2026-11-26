namespace Sim.System.Application.Contracts.User;

/// <summary>
/// 用户创建完成后的扩展初始化端口。
/// 实现方在用户记录写入后同步补齐自身模块的关联数据（例如业务模块创建人员档案），
/// 异常会向上抛出，由调用方的用户创建流程决定回滚或失败。
/// </summary>
public interface IUserCreatedHandler
{
    Task HandleAsync(UserCreatedContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// 新用户的最小公开信息，供扩展模块初始化关联数据；不包含邮箱、电话和认证字段。
/// </summary>
public sealed record UserCreatedContext(
    Guid UserId,
    string UserName,
    string? NickName,
    Guid? TenantId);
