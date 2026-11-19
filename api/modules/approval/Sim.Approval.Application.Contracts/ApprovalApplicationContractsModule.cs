using Sim.Approval.Domain.Shared;
using Volo.Abp.Modularity;

namespace Sim.Approval.Application.Contracts;

[DependsOn(typeof(ApprovalDomainSharedModule))]
public class ApprovalApplicationContractsModule : AbpModule
{
}
