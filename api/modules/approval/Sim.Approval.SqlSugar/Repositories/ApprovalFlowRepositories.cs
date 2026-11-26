using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.IRepositories;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;

namespace Sim.Approval.SqlSugar.Repositories;

public class ApprovalFlowDefinitionRepository : SqlSugarRepository<ApprovalFlowDefinition>, IApprovalFlowDefinitionRepository
{
    public ApprovalFlowDefinitionRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class ApprovalFlowNodeRepository : SqlSugarRepository<ApprovalFlowNode>, IApprovalFlowNodeRepository
{
    public ApprovalFlowNodeRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class ApprovalFlowEdgeRepository : SqlSugarRepository<ApprovalFlowEdge>, IApprovalFlowEdgeRepository
{
    public ApprovalFlowEdgeRepository(ISqlSugarClient db) : base(db)
    {
    }
}
