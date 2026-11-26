using System;
using System.Threading;

namespace Sim.Framework.Domain.Shared.Auditing;

/// <summary>
/// 当前操作的审计归属人。HTTP 请求内由 <see cref="Security.ICurrentUser"/> 提供；
/// 种子、后台任务等没有登录上下文的流程可显式指定操作者，供 SqlSugar 审计字段兜底使用。
/// </summary>
public static class AuditContext
{
    private static readonly AsyncLocal<Guid?> _currentUserId = new();

    /// <summary>
    /// 当前操作者标识。为 null 时不覆盖 SqlSugar 的默认审计行为。
    /// </summary>
    public static Guid? CurrentUserId
    {
        get => _currentUserId.Value;
        set => _currentUserId.Value = value;
    }

    /// <summary>
    /// 在指定操作者下执行审计赋值，离开作用域后恢复原值。
    /// </summary>
    public static IDisposable UseUser(Guid userId)
    {
        var previous = _currentUserId.Value;
        _currentUserId.Value = userId;
        return new RestoreScope(previous);
    }

    private sealed class RestoreScope(Guid? previous) : IDisposable
    {
        public void Dispose() => _currentUserId.Value = previous;
    }
}
