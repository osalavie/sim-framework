using Sim.Approval.Domain.Shared;
using Volo.Abp.Modularity;

namespace Sim.Approval.Domain;

[DependsOn(typeof(ApprovalDomainSharedModule))]
public class ApprovalDomainModule : AbpModule
{
}
