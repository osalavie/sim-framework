namespace Sim.System.Domain.Shared.Consts;

/// <summary>
/// 产品品牌默认值。运行时优先读取系统设置 <c>site.name</c>；
/// 此常量用于系统设置缺失时的兜底、种子数据以及无法注入配置的静态默认值。
/// </summary>
public static class BrandDefaults
{
    /// <summary>默认品牌名。</summary>
    public const string Name = "SimApp";
}
