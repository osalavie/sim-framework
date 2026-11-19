using Sim.OpenPlatform.Domain;
using Volo.Abp.Modularity;

namespace Sim.OpenPlatform.SqlSugar;

[DependsOn(typeof(SimOpenPlatformDomainModule))]
public class SimOpenPlatformSqlSugarModule : AbpModule
{
    // IOpenPlatformApplicationRepository / IOpenPlatformAccessLogRepository
    // 通过 ISqlSugarRepository 子接口约定自动注册（I{Name}Repository → {Name}Repository）。
}