namespace Sim.Framework.Domain.Shared.Entities;

/// <summary>
/// Base interface for all entities. Replaces Volo.Abp.Domain.Entities.IEntity.
/// </summary>
public interface IEntity
{
    /// <summary>Unique identifier type of the entity.</summary>
    Type IdType => typeof(object);
}

/// <summary>
/// Generic entity interface with a typed primary key. Replaces Volo.Abp.Domain.Entities.IEntity&lt;TKey&gt;.
/// </summary>
public interface IEntity<TKey> : IEntity where TKey : notnull
{
    /// <summary>Primary key of the entity.</summary>
    TKey Id { get; set; }

    /// <inheritdoc/>
    new Type IdType => typeof(TKey);
}
