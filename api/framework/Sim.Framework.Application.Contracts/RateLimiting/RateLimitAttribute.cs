namespace Sim.Framework.Application.Contracts.RateLimiting;

/// <summary>
/// 为 conventional controller 端点声明固定窗口访问频率限制。
/// 未传入的值从 <c>RateLimiting</c> 配置节读取。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RateLimitAttribute : Attribute
{
    public RateLimitAttribute(int permitLimit = 0, int windowSeconds = 0)
    {
        if (permitLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(permitLimit));
        }

        if (windowSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSeconds));
        }

        PermitLimit = permitLimit;
        WindowSeconds = windowSeconds;
    }

    /// <summary>
    /// 窗口内允许的请求数；0 表示使用配置默认值。
    /// </summary>
    public int PermitLimit { get; }

    /// <summary>
    /// 固定窗口秒数；0 表示使用配置默认值。
    /// </summary>
    public int WindowSeconds { get; }

    /// <summary>
    /// 可选的分区名。默认使用端点路由模式，使不同接口互不消耗配额。
    /// </summary>
    public string? PartitionName { get; init; }
}
