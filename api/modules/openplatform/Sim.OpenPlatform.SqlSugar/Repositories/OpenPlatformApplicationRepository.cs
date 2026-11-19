using Sim.OpenPlatform.Domain.Entities;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;

namespace Sim.OpenPlatform.SqlSugar.Repositories;

public class OpenPlatformApplicationRepository : SqlSugarRepository<OpenPlatformApplication>, IOpenPlatformApplicationRepository
{
    public OpenPlatformApplicationRepository(ISqlSugarClient db) : base(db)
    {
    }
}