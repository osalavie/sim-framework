using Sim.System.Domain.Shared;
using Volo.Abp.Modularity;

namespace Sim.System.Application.Contracts;

[DependsOn(
    typeof(SystemDomainSharedModule)
)]
public class SystemApplicationContractsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Configuration for Application.Contracts layer
    }
}
