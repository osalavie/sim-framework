using Sim.OpenPlatform.Domain.Entities;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;

namespace Sim.OpenPlatform.SqlSugar.Repositories;

public class OpenPlatformAccessLogRepository : SqlSugarRepository<OpenPlatformAccessLog>, IOpenPlatformAccessLogRepository
{
    public OpenPlatformAccessLogRepository(ISqlSugarClient db) : base(db)
    {
    }
}