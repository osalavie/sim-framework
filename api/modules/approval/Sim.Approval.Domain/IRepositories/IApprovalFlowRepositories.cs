using Sim.Approval.Domain.Entities;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.Approval.Domain.IRepositories;

public interface IApprovalFlowDefinitionRepository : ISqlSugarRepository<ApprovalFlowDefinition>
{
}

public interface IApprovalFlowNodeRepository : ISqlSugarRepository<ApprovalFlowNode>
{
}

public interface IApprovalFlowEdgeRepository : ISqlSugarRepository<ApprovalFlowEdge>
{
}
