using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.OpenPlatform.Application.Contracts;
using Sim.OpenPlatform.Application.Contracts.Dtos;
using Sim.OpenPlatform.Domain;
using Sim.OpenPlatform.Domain.Entities;
using Sim.OpenPlatform.Domain.Shared;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.OpenPlatform.Application.Services;

/// <summary>
/// 开放平台应用管理 — 管理端维护第三方接入应用及其授权范围。
/// </summary>
[Authorize]
[Permission(OpenPlatformPermissions.Manage)]
public class OpenPlatformApplicationAppService : ApplicationService, IOpenPlatformApplicationAppService
{
    private readonly ISqlSugarRepository<OpenPlatformApplication> _repository;
    private readonly IOpenPlatformSecretProtector _secretProtector;
    private readonly IOpenPlatformScopeCatalog _scopeCatalog;

    public OpenPlatformApplicationAppService(
        ISqlSugarRepository<OpenPlatformApplication> repository,
        IOpenPlatformSecretProtector secretProtector,
        IOpenPlatformScopeCatalog scopeCatalog)
    {
        _repository = repository;
        _secretProtector = secretProtector;
        _scopeCatalog = scopeCatalog;
    }

    /// <summary>获取全部第三方应用列表（按创建时间倒序）。</summary>
    /// <returns>第三方应用 DTO 列表。</returns>
    public async Task<List<OpenPlatformApplicationDto>> GetListAsync()
    {
        var apps = await _repository.GetAll();
        return apps.OrderByDescending(a => a.CreationTime).Select(MapToDto).ToList();
    }

    public async Task<OpenPlatformApplicationDto> GetAsync(Guid id)
    {
        var app = await _repository.GetAsync(id)
            ?? throw new BusinessException(OpenPlatformErrorCodes.NotFound, "Open platform application not found.");
        return MapToDto(app);
    }

    public async Task<CreateOpenPlatformApplicationResultDto> CreateAsync(CreateOpenPlatformApplicationDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 120)
            throw new BusinessException(OpenPlatformErrorCodes.InvalidName, "Application name is required.");

        await ValidateScopesAsync(input.Scopes);

        var plainSecret = OpenPlatformCredentialGenerator.GenerateSecret();
        var application = new OpenPlatformApplication
        {
            Name = input.Name.Trim(),
            AppId = OpenPlatformCredentialGenerator.GenerateAppId(),
            ProtectedAppSecret = _secretProtector.Protect(plainSecret),
            Description = input.Description?.Trim(),
            IsActive = true,
            Scopes = SerializeScopes(input.Scopes),
            AllowedIps = SerializeList(input.AllowedIps)
        };

        await _repository.InsertAsync(application);

        return new CreateOpenPlatformApplicationResultDto
        {
            Id = application.Id,
            AppId = application.AppId,
            AppSecret = plainSecret
        };
    }

    /// <summary>更新第三方应用信息（仅更新传入的非空字段）。</summary>
    /// <param name="id">应用唯一标识。</param>
    /// <param name="input">应用更新数据。</param>
    /// <returns>更新后的第三方应用 DTO。</returns>
    public async Task<OpenPlatformApplicationDto> UpdateAsync(Guid id, UpdateOpenPlatformApplicationDto input)
    {
        var app = await _repository.GetAsync(id)
            ?? throw new BusinessException(OpenPlatformErrorCodes.NotFound, "Open platform application not found.");

        if (input.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 120)
                throw new BusinessException(OpenPlatformErrorCodes.InvalidName, "Application name is required.");
            app.Name = input.Name.Trim();
        }
        if (input.Description is not null)
            app.Description = input.Description.Trim();
        if (input.Scopes is not null)
        {
            await ValidateScopesAsync(input.Scopes);
            app.Scopes = SerializeScopes(input.Scopes);
        }
        if (input.AllowedIps is not null)
            app.AllowedIps = SerializeList(input.AllowedIps);

        await _repository.UpdateAsync(app);
        return MapToDto(app);
    }

    public async Task EnableAsync(Guid id)
    {
        var app = await GetEntityAsync(id);
        app.IsActive = true;
        await _repository.UpdateAsync(app);
    }

    public async Task DisableAsync(Guid id)
    {
        var app = await GetEntityAsync(id);
        app.IsActive = false;
        await _repository.UpdateAsync(app);
    }

    public async Task DeleteAsync(Guid id)
    {
        var app = await GetEntityAsync(id);
        await _repository.DeleteAsync(app.Id);
    }

    /// <summary>重置第三方应用的 AppSecret，旧密钥立即失效。</summary>
    /// <param name="id">应用唯一标识。</param>
    /// <returns>重置结果，包含明文 AppSecret（仅此一次返回）。</returns>
    public async Task<ResetOpenPlatformSecretResultDto> ResetSecretAsync(Guid id)
    {
        var app = await GetEntityAsync(id);
        var plainSecret = OpenPlatformCredentialGenerator.GenerateSecret();
        app.ProtectedAppSecret = _secretProtector.Protect(plainSecret);
        await _repository.UpdateAsync(app);

        return new ResetOpenPlatformSecretResultDto { AppSecret = plainSecret };
    }

    private async Task<OpenPlatformApplication> GetEntityAsync(Guid id)
        => await _repository.GetAsync(id)
           ?? throw new BusinessException(OpenPlatformErrorCodes.NotFound, "Open platform application not found.");

    private static string? SerializeScopes(List<string> scopes)
        => scopes is { Count: > 0 }
            ? JsonSerializer.Serialize(scopes.Distinct().ToList())
            : null;

    private async Task ValidateScopesAsync(List<string> scopes)
    {
        var allowed = (await _scopeCatalog.GetGrantableScopesAsync())
            .Select(scope => scope.Code)
            .ToHashSet(StringComparer.Ordinal);
        if (!OpenPlatformScopePolicy.AreGrantable(scopes, allowed))
            throw new BusinessException(OpenPlatformErrorCodes.InvalidScope, "Invalid application scope.");
    }

    private static List<string> DeserializeScopes(string? scopes)
    {
        if (string.IsNullOrWhiteSpace(scopes))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(scopes) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static string? SerializeList(List<string> values)
        => values is { Count: > 0 }
            ? JsonSerializer.Serialize(values.Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToList())
            : null;

    private static List<string> DeserializeList(string? values)
    {
        if (string.IsNullOrWhiteSpace(values))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(values) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static OpenPlatformApplicationDto MapToDto(OpenPlatformApplication app) => new()
    {
        Id = app.Id,
        Name = app.Name,
        AppId = app.AppId,
        Description = app.Description,
        IsActive = app.IsActive,
        Scopes = DeserializeScopes(app.Scopes),
        AllowedIps = DeserializeList(app.AllowedIps),
        CreationTime = app.CreationTime,
        CreatorId = app.CreatorId,
        LastModificationTime = app.LastModificationTime,
        LastModifierId = app.LastModifierId
    };
}
