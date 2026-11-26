using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Domain.Shared.Numbering;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.Numbering;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.Numbering;

namespace Sim.System.Application.Numbering;

[Authorize]
[Permission(SystemManagementPermissions.NumberingManage)]
public sealed class BusinessNumberDefinitionService(
    ISqlSugarRepository<BusinessNumberDefinitionEntity> repository)
    : ApplicationService, IBusinessNumberDefinitionService, IBusinessNumberDefinitionStore,
        IBusinessNumberDefinitionSeeder
{
    /// <summary>
    /// 确保指定业务编号定义存在；已存在时不做任何修改。
    /// </summary>
    public async Task EnsureAsync(
        string tenantScope,
        string businessKey,
        string displayName,
        string? description,
        string format,
        long initialSequence,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exists = await repository.GetAsync(item =>
            item.TenantScope == tenantScope && item.BusinessKey == businessKey);
        if (exists != null)
        {
            return;
        }

        await repository.InsertAsync(new BusinessNumberDefinitionEntity(Guid.NewGuid())
        {
            TenantScope = tenantScope,
            BusinessKey = businessKey,
            DisplayName = displayName,
            Description = description,
            Format = format,
            InitialSequence = initialSequence,
            Enabled = true
        });
    }

    /// <summary>
    /// 获取当前租户的业务编号定义列表。
    /// </summary>
    public async Task<List<BusinessNumberDefinitionDto>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (await repository.GetAll())
            .OrderBy(item => item.DisplayName)
            .ThenBy(item => item.BusinessKey)
            .Select(Map)
            .ToList();
    }

    /// <summary>
    /// 创建业务编号定义。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<BusinessNumberDefinitionDto> CreateAsync(
        CreateBusinessNumberDefinitionDto input,
        CancellationToken cancellationToken = default)
    {
        Validate(input.BusinessKey, input.DisplayName, input.Format, input.InitialSequence);
        var businessKey = NormalizeBusinessKey(input.BusinessKey);
        var tenantScope = BusinessNumberDefinitionEntity.CurrentTenantScope();
        var exists = (await repository.GetAll())
            .Any(item => item.TenantScope == tenantScope && item.BusinessKey == businessKey);
        if (exists)
        {
            throw new BusinessException(SystemErrorCodes.BusinessNumberDefinitionAlreadyExists);
        }

        var entity = new BusinessNumberDefinitionEntity(Guid.NewGuid())
        {
            TenantScope = tenantScope,
            BusinessKey = businessKey,
            DisplayName = input.DisplayName.Trim(),
            Description = NormalizeOptional(input.Description),
            Format = input.Format.Trim(),
            InitialSequence = input.InitialSequence,
            Enabled = input.Enabled
        };
        await repository.InsertAsync(entity);
        return Map(entity);
    }

    /// <summary>
    /// 更新指定的业务编号定义。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<BusinessNumberDefinitionDto> UpdateAsync(
        Guid id,
        UpdateBusinessNumberDefinitionDto input,
        CancellationToken cancellationToken = default)
    {
        Validate("managed", input.DisplayName, input.Format, input.InitialSequence);
        var entity = await repository.GetAsync(id)
            ?? throw new BusinessException(SystemErrorCodes.BusinessNumberDefinitionNotFound);
        entity.DisplayName = input.DisplayName.Trim();
        entity.Description = NormalizeOptional(input.Description);
        entity.Format = input.Format.Trim();
        entity.InitialSequence = input.InitialSequence;
        entity.Enabled = input.Enabled;
        await repository.UpdateAsync(entity);
        return Map(entity);
    }

    async Task<BusinessNumberDefinition?> IBusinessNumberDefinitionStore.FindAsync(
        string businessKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeBusinessKey(businessKey);
        var tenantScope = BusinessNumberDefinitionEntity.CurrentTenantScope();
        var entity = (await repository.GetAll())
            .FirstOrDefault(item => item.TenantScope == tenantScope && item.BusinessKey == normalized);
        return entity is null
            ? null
            : new BusinessNumberDefinition(
                entity.BusinessKey,
                entity.Format,
                entity.InitialSequence,
                entity.Enabled);
    }

    private static void Validate(string businessKey, string displayName, string format, long initialSequence)
    {
        if (string.IsNullOrWhiteSpace(businessKey) ||
            string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(format) ||
            initialSequence <= 0)
        {
            throw new BusinessException(SystemErrorCodes.InvalidBusinessNumberDefinition);
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            NumberFormatRenderer.GetSequenceScope(format, now);
            NumberFormatRenderer.Render(format, NormalizeBusinessKey(businessKey), now, initialSequence);
        }
        catch (ArgumentException)
        {
            throw new BusinessException(SystemErrorCodes.InvalidBusinessNumberDefinition);
        }
        catch (FormatException)
        {
            throw new BusinessException(SystemErrorCodes.InvalidBusinessNumberDefinition);
        }
    }

    private static string NormalizeBusinessKey(string businessKey)
    {
        var normalized = businessKey.Trim().ToLowerInvariant();
        if (normalized.Length is < 1 or > 128 ||
            normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':')))
        {
            throw new BusinessException(SystemErrorCodes.InvalidBusinessNumberDefinition);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BusinessNumberDefinitionDto Map(BusinessNumberDefinitionEntity entity) => new()
    {
        Id = entity.Id,
        BusinessKey = entity.BusinessKey,
        DisplayName = entity.DisplayName,
        Description = entity.Description,
        Format = entity.Format,
        InitialSequence = entity.InitialSequence,
        Enabled = entity.Enabled
    };
}
