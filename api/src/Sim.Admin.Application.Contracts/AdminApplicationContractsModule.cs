using Sim.Admin.Domain.Shared;
using Volo.Abp.Modularity;

namespace Sim.Admin.Application.Contracts;

[DependsOn(
    typeof(AdminDomainSharedModule)
)]
public class AdminApplicationContractsModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AdminDtoExtensions.Configure();
    }
}
