using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.IServices;
using Sim.Framework.Application.Extensions;
using Sim.Framework.Domain.Shared.Entities;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.Framework.Application.Services;

/// <summary>
/// 通用 CRUD 应用服务基类，实现了 <see cref="ICustomCrudAppService{TKey, TEntityDto, TEntityCreateDto, TEntityUpdateDto}"/> 的标准 CRUD 方法。
/// 派生类只需提供实体与 DTO 之间的映射，即可对外暴露标准的 CRUD HTTP API。
/// </summary>
public abstract class CrudAppService<TKey, TEntity, TEntityDto, TEntityCreateDto, TEntityUpdateDto>(
    ISqlSugarRepository<TEntity> repository)
    : ApplicationService, ICustomCrudAppService<TKey, TEntityDto, TEntityCreateDto, TEntityUpdateDto>
    where TKey : notnull
    where TEntity : class, IEntity, new()
    where TEntityDto : EntityDto<TKey>, new()
    where TEntityUpdateDto : EntityDto<TKey>, new()
{
    protected ISqlSugarRepository<TEntity> Repository => repository;

    protected Task<TEntity> MapToEntityAsync(TEntityDto input)
    {
        return Task.FromResult(ObjectMapper.Map(input, new TEntity()));
    }

    protected Task<TEntity> MapToEntityAsync(TEntityCreateDto input)
    {
        return Task.FromResult(ObjectMapper.Map(input, new TEntity()));
    }

    protected Task<TEntity> MapToEntityAsync(TEntityUpdateDto input)
    {
        return Task.FromResult(ObjectMapper.Map(input, new TEntity()));
    }

    protected TEntity MapToEntity(TEntityDto input)
    {
        return ObjectMapper.Map(input, new TEntity());
    }

    protected TEntity MapToEntity(TEntityCreateDto input)
    {
        return ObjectMapper.Map(input, new TEntity());
    }

    protected TEntity MapToEntity(TEntityUpdateDto input)
    {
        return ObjectMapper.Map(input, new TEntity());
    }

    protected Task<TEntityDto> MapToDtoAsync(TEntity entity)
    {
        return Task.FromResult(ObjectMapper.Map(entity, new TEntityDto()));
    }

    protected TEntityDto MapToDto(TEntity entity)
    {
        return ObjectMapper.Map(entity, new TEntityDto());
    }

    public virtual async Task<TEntityDto> GetAsync(TKey id)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }
        var entity = await Repository.GetAsync(id);
        if (entity == null)
        {
            throw new BusinessException($"Entity with id {id} not found");
        }
        return ObjectMapper.Map(entity, new TEntityDto());
    }

    public virtual async Task<PagedResultDto<TEntityDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = Repository.GetQueryable();

        // 先获取总数（排序前）
        var totalCount = await queryable.CountAsync();

        // 应用排序（SqlSugar 原生支持字符串排序，支持多列）
        queryable = queryable.ApplySorting(input.Sorting);

        // 应用分页
        queryable = queryable
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var entities = await queryable.ToListAsync();
        var dtos = entities.Select(MapToDto).ToList();

        return new PagedResultDto<TEntityDto>(totalCount, dtos);
    }

    public virtual async Task<TEntityDto> CreateAsync(TEntityCreateDto input)
    {
        TEntity entity = MapToEntity(input);
        TEntity createdEntity = await Repository.InsertWithReturnAsync(entity);
        return ObjectMapper.Map(createdEntity, new TEntityDto());
    }

    public virtual async Task<TEntityDto> UpdateAsync(TEntityUpdateDto input)
    {
        ArgumentNullException.ThrowIfNull(input.Id);

        TEntity? existingEntity = await Repository.GetAsync(input.Id);
        if (existingEntity == null)
        {
            throw new BusinessException($"Entity with id {input.Id} not found");
        }
        existingEntity = ObjectMapper.Map(input, existingEntity);
        TEntity updatedEntity = await Repository.UpdateReturnAsync(existingEntity);
        return MapToDto(updatedEntity);
    }

    public virtual async Task<bool> DeleteAsync(TKey id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Repository.DeleteAsync(id);
    }
}

public class CrudAppService<TEntity, TEntityDto, TEntityCreateDto, TEntityUpdateDto>(
    ISqlSugarRepository<TEntity> repository)
    : CrudAppService<Guid, TEntity, TEntityDto, TEntityCreateDto, TEntityUpdateDto>(repository)
    where TEntity : class, IEntity<Guid>, new()
    where TEntityDto : EntityDto<Guid>, new()
    where TEntityUpdateDto : EntityDto<Guid>, new()
{
}
