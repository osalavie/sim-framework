using System.Linq.Expressions;
using Sim.Framework.Domain.Shared.Entities;
using SqlSugar;

namespace Sim.SqlSugar.Core.Repositories
{
        public interface ISqlSugarRepository<TEntity> where TEntity : class, IEntity, new()
        {
                ISugarQueryable<TEntity> GetQueryable();
                Task<TEntity?> GetAsync(dynamic id);
                Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> expression);
                Task<List<TEntity>> GetAll();
                Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> expression);
                Task<int> UpdateAsync(TEntity entity);
                Task<TEntity> UpdateReturnAsync(TEntity entity);

                /// <summary>
                /// 按字段更新实体，如果有以下划线开头的字段需要设置toSql为true
                /// </summary>
                /// <param name="entity"></param>
                /// <param name="properties"></param>
                /// <returns></returns>
                Task<int> UpdateByFieldsAsync(TEntity entity, Expression<Func<TEntity, object>> properties, bool ignoreNullColumns = true, bool toSql = false);

                Task<int> UpdateManyAsync(List<TEntity> entities);
                Task<int> UpdateManyByFieldsAsync(List<TEntity> entities, Expression<Func<TEntity, object>> properties);
                Task<int> InsertAsync(TEntity entity);
                Task<TEntity> InsertWithReturnAsync(TEntity entity);
                Task<int> InsertManyAsync(List<TEntity> entities);
                Task<bool> DeleteAsync(dynamic id);
                Task<int> DeleteAsync(Expression<Func<TEntity, bool>> expression);
                Task<int> DeleteManyAsync(List<TEntity> entities);
        }
}
