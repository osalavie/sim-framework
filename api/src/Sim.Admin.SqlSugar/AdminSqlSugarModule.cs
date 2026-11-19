using Sim.Admin.Domain;
using Sim.SqlSugar.Core;
using Volo.Abp.Modularity;

namespace Sim.Admin.SqlSugar;

[DependsOn(
    typeof(AdminDomainModule),
    typeof(SqlSugarCoreModule)
)]
public class AdminSqlSugarModule : AbpModule
{
  public override void ConfigureServices(ServiceConfigurationContext context)
  {
    // Configuration for Sim.SqlSugar can be added here
    // For example, you can configure the connection string or other settings
    // Additional configurations can be added as needed
  }
  public override void OnApplicationInitialization(Volo.Abp.ApplicationInitializationContext context)
  {
    // Initialization logic for Sim.SqlSugar can be added here
    // For example, you can seed data or perform other startup tasks
  }
}
