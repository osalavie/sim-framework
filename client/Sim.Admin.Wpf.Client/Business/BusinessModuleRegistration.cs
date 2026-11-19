namespace Sim.Admin.Wpf.Client.Business;

/// <summary>
/// 业务模块组合根。基座仓保持为空，业务仓只在此追加自己的模块；
/// 业务源码由 BusinessSources.props 纳入基座工程编译。
/// </summary>
public static class BusinessModuleRegistration
{
    public static IReadOnlyList<IBusinessClientModule> Modules => [];
}
