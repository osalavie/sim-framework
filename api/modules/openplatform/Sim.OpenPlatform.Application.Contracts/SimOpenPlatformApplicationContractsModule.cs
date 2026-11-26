using Sim.OpenPlatform.Domain.Shared;
using Volo.Abp.Modularity;

namespace Sim.OpenPlatform.Application.Contracts;

[DependsOn(typeof(SimOpenPlatformDomainSharedModule))]
public class SimOpenPlatformApplicationContractsModule : AbpModule
{
}