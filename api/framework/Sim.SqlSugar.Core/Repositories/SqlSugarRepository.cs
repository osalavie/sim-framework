using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sim.Framework.Domain;
using Sim.Framework.Domain.Shared.Entities;
using Sim.Framework.Domain.Shared.MultiTenancy;
using SqlSugar;

namespace Sim.SqlSugar.Core.Repositories
{
    public class SqlSugarRepository<TEntity> : SimpleClient<TEntity>, ISqlSugarRepository<TEntity>, IRepository<TEntity>
        where TEntity : class, IEntity, new()
    {
        public ILogger<TEntity> Logger { get; set; } = NullLogger<TEntity>.Instance;

        public SqlSugarRepository(ISqlSugarClient db) : base(db)
        {
            base.Context = db;
        }

        public ISugarQueryable<TEntity> GetQueryable()
        {
            return ApplyTenantFilter(AsTenant().QueryableWithAttr<TEntity>());
        }
        public async Task<TEntity?> GetAsync(dynamic id)
        {
            var queryable = ApplyTenantFilter(AsTenant().QueryableWithAttr<TEntity>());
            var result = await queryable.InSingleAsync(id);
            if (result != null)
            {
                Context.Tracking(result);
            }
            return result;
        }
        public async Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> expression)
        {
            var queryable = ApplyTenantFilter(AsTenant().QueryableWithAttr<TEntity>());
            var result = await queryable.FirstAsync(expression);
            if (result != null)
            {
                Context.Tracking(result);
            }
            return result;
        }

        public async Task<List<TEntity>> GetAll()
        {
            var result = await ApplyTenantFilter(AsTenant().QueryableWithAttr<TEntity>()).ToListAsync();
            Context.Tracking(result);
            return result;
        }

        public override async Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> expression)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var result = await ApplyTenantFilter(AsTenant().QueryableWithAttr<TEntity>()).Where(expression).ToListAsync();
            Context.Tracking(result);
            return result ?? [];
        }

        public async Task<int> UpdateByFieldsAsync(TEntity entity, Expression<Func<TEntity, object>> properties, bool ignoreNullColumns = true, bool toSql = false)
        {
            int res = 0;
            var updateable = AsTenant()
                .UpdateableWithAttr(entity)
                .IgnoreNullColumns(ignoreNullColumns)
                .UpdateColumns(properties);

            if (!CheckUpdateable(updateable))
            {
                Logger.LogWarning("No columns need to be updated");
                return 0;
            }

            if (toSql)
            {
                string sql = updateable.ToSqlString();
                res = await Context.Ado.ExecuteCommandAsync(sql);
            }
            else
            {
                res = await updateable.ExecuteCommandAsync();
            }

            return res;
        }

        public async Task<bool> DeleteAsync(dynamic id)
        {
            if (!TenantFilterApplies)
            {
                return await DeleteByIdAsync(id);
            }

            var existing = await GetAsync(id);
            return existing != null && await DeleteByIdAsync(id);
        }

        private bool CheckUpdateable(IUpdateable<TEntity> updateable)
        {
            string sql = updateable.ToSqlString();
            var emptySetRegex = new System.Text.RegularExpressions.Regex(@"SET\s*WHERE",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (emptySetRegex.IsMatch(sql))
            {
                return false;
            }

            return true;
        }

        public new async Task<int> UpdateAsync(TEntity entity)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            return await ApplyTenantFilter(AsTenant().UpdateableWithAttr(entity)).ExecuteCommandAsync();
        }

        public async Task<TEntity> UpdateReturnAsync(TEntity entity)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var result = await ApplyTenantFilter(AsTenant().UpdateableWithAttr(entity)).ExecuteReturnEntityAsync();
            if (result != null)
            {
                Context.Tracking(result);
            }
            return result!;
        }

        public async Task<int> UpdateByFieldsAsync(TEntity entity, Expression<Func<TEntity, object>> properties)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var updateable = ApplyTenantFilter(AsTenant().UpdateableWithAttr(entity)).UpdateColumns(properties);
            return await updateable.ExecuteCommandAsync();
        }

        public async Task<int> UpdateByFields2Async(TEntity entity, Expression<Func<TEntity, object>> properties)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var sql = ApplyTenantFilter(AsTenant().UpdateableWithAttr(entity)).UpdateColumns(properties).ToSqlString();
            return await Context.Ado.ExecuteCommandAsync(sql);
        }

        public async Task<int> UpdateManyAsync(List<TEntity> entities)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var result = await ApplyTenantFilter(AsTenant().UpdateableWithAttr(entities)).PageSize(1000).ExecuteCommandAsync();
            return result;
        }

        public async Task<int> UpdateManyByFieldsAsync(List<TEntity> entities, Expression<Func<TEntity, object>> properties)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var result = await ApplyTenantFilter(AsTenant().UpdateableWithAttr(entities)).UpdateColumns(properties).PageSize(1000).ExecuteCommandAsync();
            return result;
        }

        public new async Task<int> InsertAsync(TEntity entity)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var result = await AsTenant().InsertableWithAttr(entity).ExecuteCommandAsync();
            return result;
        }

        public async Task<TEntity> InsertWithReturnAsync(TEntity entity)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var result = await AsTenant().InsertableWithAttr(entity).ExecuteReturnEntityAsync();
            if (result != null)
            {
                Context.Tracking(result);
            }
            return result!;
        }

        public async Task<int> InsertManyAsync(List<TEntity> entities)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var result = await AsTenant().InsertableWithAttr(entities).PageSize(1000).ExecuteCommandAsync();
            return result;
        }

        public new async Task<int> DeleteAsync(Expression<Func<TEntity, bool>> expression)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var deleteable = ApplyTenantFilter(AsTenant().DeleteableWithAttr<TEntity>().Where(expression));
            var result = await deleteable.ExecuteCommandAsync();
            return result;
        }
        public async Task<int> DeleteManyAsync(List<TEntity> entities)
        {
            Logger.LogDebug($"DB ContextID: {Context.ContextID}");
            var result = await ApplyTenantFilter(AsTenant().DeleteableWithAttr(entities)).ExecuteCommandAsync();
            return result;
        }

        // ── IRepository<TEntity> explicit implementation (Framework abstraction) ──────────

        async Task<TEntity?> IRepository<TEntity>.FindAsync(object id)
        {
            return await GetAsync(id);
        }

        async Task<List<TEntity>> IRepository<TEntity>.QueryAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await GetListAsync(predicate);
        }

        async Task<TEntity> IRepository<TEntity>.AddAsync(TEntity entity)
        {
            return await InsertWithReturnAsync(entity);
        }

        async Task<TEntity> IRepository<TEntity>.SaveAsync(TEntity entity)
        {
            return await UpdateReturnAsync(entity);
        }

        async Task IRepository<TEntity>.RemoveAsync(object id)
        {
            await DeleteAsync(id);
        }

        async Task IRepository<TEntity>.AddManyAsync(List<TEntity> entities)
        {
            await InsertManyAsync(entities);
        }

        // ── Multi-tenant query filter ──────────────────────────────────────────

        /// <summary>
        /// When the current tenant is set (non-host), automatically filters
        /// queries to only return data belonging to that tenant.
        /// Entities not implementing IMultiTenant are excluded from filtering.
        /// </summary>
        private static ISugarQueryable<TEntity> ApplyTenantFilter(ISugarQueryable<TEntity> queryable)
        {
            return TenantFilterApplies ? queryable.Where(BuildTenantPredicate()) : queryable;
        }

        protected static IUpdateable<TEntity> ApplyTenantFilter(IUpdateable<TEntity> updateable)
        {
            return TenantFilterApplies ? updateable.Where(BuildTenantPredicate()) : updateable;
        }

        private static IDeleteable<TEntity> ApplyTenantFilter(IDeleteable<TEntity> deleteable)
        {
            return TenantFilterApplies ? deleteable.Where(BuildTenantPredicate()) : deleteable;
        }

        private static bool TenantFilterApplies =>
            TenantContext.CurrentTenantId.HasValue &&
            typeof(TEntity).GetInterface(typeof(IMultiTenant).FullName!) != null;

        private static Expression<Func<TEntity, bool>> BuildTenantPredicate()
        {
            var tenantId = TenantContext.CurrentTenantId!.Value;
            // 使用表达式而非 SQL 字符串，确保 SqlSugar 按实体映射生成 tenant_id 等实际列名。
            var parameter = Expression.Parameter(typeof(TEntity), "entity");
            var tenantIdProperty = Expression.Property(parameter, nameof(IMultiTenant.TenantId));
            var body = Expression.Equal(tenantIdProperty, Expression.Constant(tenantId, typeof(Guid?)));
            return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
        }
    }
}
