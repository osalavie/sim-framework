using Sim.OpenPlatform.Domain.Shared;
using Volo.Abp.Modularity;

namespace Sim.OpenPlatform.Domain;

[DependsOn(typeof(SimOpenPlatformDomainSharedModule))]
public class SimOpenPlatformDomainModule : AbpModule
{
}