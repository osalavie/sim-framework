using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Business;

/// <summary>业务工作区可用的会话能力；API 已登录并负责令牌刷新。</summary>
public sealed class BusinessClientContext
{
    public required ApiClient Api { get; init; }

    public required Guid UserId { get; init; }

    public required string UserName { get; init; }

    public required Action<string?> EndSession { get; init; }
}
