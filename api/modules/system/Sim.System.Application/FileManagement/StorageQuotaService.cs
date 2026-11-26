using Sim.System.Application.Contracts.FileManagement;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileManagement;

public sealed class StorageQuotaService(
    ISqlSugarRepository<StorageQuotaEntity> quotaRepository,
    ISqlSugarRepository<FileMetadataEntity> metadataRepository) : IStorageQuotaService
{
    private const long DefaultUserQuotaBytes = 1024L * 1024 * 1024; // 1 GB default
    private const long DefaultTenantQuotaBytes = 10L * 1024 * 1024 * 1024; // 10 GB default

    public async Task<StorageQuotaDto> GetUsageAsync(
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var quota = await GetOrCreateQuotaAsync(tenantId, userId, cancellationToken);

        // Recalculate actual usage from file metadata for accuracy
        var query = metadataRepository.GetQueryable();
        if (userId.HasValue)
        {
            query = query.Where(f => f.CreatorId == userId.Value);
        }

        // 单行聚合：SqlSugar 的 GroupBy(常量) 在 PostgreSQL 下会生成缺少分组键的 "GROUP BY"，
        // 与 First 追加的 LIMIT 相邻后直接语法错误（42601），因此这里不做 GROUP BY。
        // 聚合函数对空集合返回一行 NULL，SqlSugar 会映射为 0。
        var stats = await query
            .Select(f => new { TotalLength = SqlFunc.AggregateSum(f.Length), Count = SqlFunc.AggregateCount(f.Id) })
            .FirstAsync();

        var usedBytes = stats?.TotalLength ?? 0;
        var fileCount = stats?.Count ?? 0;

        // Update denormalized fields if they've drifted
        if (quota.UsedBytes != usedBytes || quota.FileCount != fileCount)
        {
            quota.UsedBytes = usedBytes;
            quota.FileCount = fileCount;
            await quotaRepository.UpdateAsync(quota);
        }

        return new StorageQuotaDto
        {
            TenantId = tenantId,
            UserId = userId,
            UsedBytes = usedBytes,
            QuotaBytes = quota.QuotaBytes,
            FileCount = fileCount
        };
    }

    public async Task<bool> CheckQuotaAsync(
        long additionalBytes,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var quota = await GetOrCreateQuotaAsync(tenantId, userId, cancellationToken);

        // QuotaBytes == 0 means unlimited
        if (quota.QuotaBytes == 0)
        {
            return true;
        }

        return quota.UsedBytes + additionalBytes <= quota.QuotaBytes;
    }

    public async Task SetQuotaAsync(
        long quotaBytes,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (quotaBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quotaBytes), "Quota cannot be negative. Use 0 for unlimited.");
        }

        var quota = await GetOrCreateQuotaAsync(tenantId, userId, cancellationToken);
        quota.QuotaBytes = quotaBytes;
        await quotaRepository.UpdateAsync(quota);
    }

    private async Task<StorageQuotaEntity> GetOrCreateQuotaAsync(
        Guid? tenantId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var query = quotaRepository.GetQueryable();
        if (tenantId.HasValue && userId.HasValue)
        {
            query = query.Where(q => q.TenantId == tenantId.Value && q.UserId == userId.Value);
        }
        else if (tenantId.HasValue)
        {
            query = query.Where(q => q.TenantId == tenantId.Value && q.UserId == null);
        }
        else if (userId.HasValue)
        {
            query = query.Where(q => q.TenantId == null && q.UserId == userId.Value);
        }
        else
        {
            query = query.Where(q => q.TenantId == null && q.UserId == null);
        }

        var quota = await query.FirstAsync(cancellationToken);
        if (quota != null)
        {
            return quota;
        }

        // Create with default quota
        quota = new StorageQuotaEntity
        {
            TenantId = tenantId,
            UserId = userId,
            QuotaBytes = userId.HasValue ? DefaultUserQuotaBytes : DefaultTenantQuotaBytes,
            UsedBytes = 0,
            FileCount = 0
        };
        await quotaRepository.InsertAsync(quota);
        return quota;
    }
}
