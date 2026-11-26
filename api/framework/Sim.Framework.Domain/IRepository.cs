using System.Linq.Expressions;
using Sim.Framework.Domain.Shared.Entities;

namespace Sim.Framework.Domain;

/// <summary>
/// Framework-level repository abstraction — the minimal contract every Domain repository must honour.
/// Domain services and managers depend on this interface, never on ORM-specific types.
/// 
/// Method names are deliberately distinct from ISqlSugarRepository to avoid ambiguity
/// when a single concrete interface inherits both abstractions.
/// </summary>
/// <typeparam name="TEntity">A Sim entity type.</typeparam>
public interface IRepository<TEntity> where TEntity : class, IEntity, new()
{
    /// <summary>Retrieve a single entity by its primary key.</summary>
    Task<TEntity?> FindAsync(object id);

    /// <summary>Retrieve entities matching a predicate.</summary>
    Task<List<TEntity>> QueryAsync(Expression<Func<TEntity, bool>> predicate);

    /// <summary>Persist a new entity and return it (may include server-generated values).</summary>
    Task<TEntity> AddAsync(TEntity entity);

    /// <summary>Persist changes to an existing entity and return the updated instance.</summary>
    Task<TEntity> SaveAsync(TEntity entity);

    /// <summary>Remove an entity by its primary key.</summary>
    Task RemoveAsync(object id);

    /// <summary>Bulk-insert a batch of entities.</summary>
    Task AddManyAsync(List<TEntity> entities);
}
