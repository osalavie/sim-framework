using Volo.Abp.Modularity;

namespace Sim.Admin.HttpApi.Host;

/// <summary>
/// 业务模块组合根。基座仓保持为空，业务仓在此追加业务模块依赖。
/// 新增业务模块时同步执行三步：
///   1. 在 Business/BusinessProjects.props 追加工程引用；
///   2. 在下方 partial 类上追加 [DependsOn]；
///   3. 在 appsettings.json 的 Swagger:Documents 追加业务文档分组。
/// </summary>
// 示例：
// [DependsOn(
//     typeof(Sim.Qms.Application.QmsApplicationModule),
//     typeof(Sim.Qms.SqlSugar.QmsSqlSugarModule))]
public partial class AdminHttpApiHostModule
{
}
