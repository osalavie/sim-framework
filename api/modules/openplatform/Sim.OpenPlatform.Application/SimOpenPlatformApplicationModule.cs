using Microsoft.Extensions.DependencyInjection;
using Sim.OpenPlatform.Application.Contracts;
using Sim.OpenPlatform.Application.Security;
using Sim.OpenPlatform.Application.Seeds;
using Sim.Framework.Domain.Shared.Data;
using Sim.OpenPlatform.SqlSugar;
using Volo.Abp.Modularity;

namespace Sim.OpenPlatform.Application;

[DependsOn(
    typeof(SimOpenPlatformApplicationContractsModule),
    typeof(SimOpenPlatformSqlSugarModule))]
public class SimOpenPlatformApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<IOpenPlatformSecretProtector, OpenPlatformSecretProtector>();
        context.Services.AddTransient<IModuleSeedContributor, OpenPlatformModuleSeedContributor>();
    }
}
