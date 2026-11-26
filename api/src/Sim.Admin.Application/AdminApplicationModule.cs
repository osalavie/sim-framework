using Sim.Framework.Core;
using Sim.Admin.Application.Contracts;
using Sim.Admin.Domain;
using Sim.System.Application;
using Volo.Abp.Modularity;

namespace Sim.Admin.Application;

[DependsOn(
    typeof(AdminDomainModule),
    typeof(AdminApplicationContractsModule),
    typeof(SystemApplicationModule),
    typeof(FrameworkCoreModule)
    )]
public class AdminApplicationModule : AbpModule
{
}
