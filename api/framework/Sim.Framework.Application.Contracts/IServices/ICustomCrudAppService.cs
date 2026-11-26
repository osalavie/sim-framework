using Sim.Framework.Application.Contracts.Dtos;

namespace Sim.Framework.Application.Contracts.IServices;

/// <summary>
/// 通用 CRUD 应用服务契约，提供 GetAsync / GetListAsync / CreateAsync / UpdateAsync / DeleteAsync 五个标准方法。
/// 实现此接口的应用服务会被常规控制器约定自动暴露为 HTTP API。
/// </summary>
public interface ICustomCrudAppService<TKey, TEntityDto, TEntityCreateDto, TEntityUpdateDto> : IApplicationService
    where TKey : notnull
    where TEntityDto : EntityDto<TKey>, new()
    where TEntityUpdateDto : EntityDto<TKey>, new()
{
    /// <summary>
    /// 根据主键获取单个实体 DTO。
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <returns>对应的实体 DTO</returns>
    Task<TEntityDto> GetAsync(TKey id);

    /// <summary>
    /// 分页获取实体 DTO 列表。
    /// </summary>
    /// <param name="input">分页、排序请求参数</param>
    /// <returns>分页结果，包含总记录数与当前页数据</returns>
    Task<PagedResultDto<TEntityDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    /// <summary>
    /// 创建实体。
    /// </summary>
    /// <param name="input">创建实体所需的数据</param>
    /// <returns>创建后的实体 DTO</returns>
    Task<TEntityDto> CreateAsync(TEntityCreateDto input);

    /// <summary>
    /// 更新实体。
    /// </summary>
    /// <param name="input">包含主键的实体更新数据</param>
    /// <returns>更新后的实体 DTO</returns>
    Task<TEntityDto> UpdateAsync(TEntityUpdateDto input);

    /// <summary>
    /// 根据主键删除实体。
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <returns>删除成功返回 true，否则返回 false</returns>
    Task<bool> DeleteAsync(TKey id);
}
