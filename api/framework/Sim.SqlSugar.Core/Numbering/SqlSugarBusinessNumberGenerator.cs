using System.Security.Cryptography;
using System.Text;
using Sim.Framework.Domain;
using Sim.Framework.Domain.Shared.Numbering;
using SqlSugar;

namespace Sim.SqlSugar.Core.Numbering;

/// <summary>
/// Database-backed number generator using optimistic updates so all application instances share one sequence.
/// </summary>
public sealed class SqlSugarBusinessNumberGenerator(
    ISqlSugarClient database,
    IBusinessNumberDefinitionStore definitions) : IBusinessNumberGenerator
{
    private const int MaxAllocationAttempts = 16;

    public async Task<string> GenerateAsync(
        NumberGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var businessKey = NormalizeBusinessKey(request.BusinessKey);
        var definition = await definitions.FindAsync(businessKey, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No managed business-number definition exists for '{businessKey}'.");
        if (!definition.Enabled)
        {
            throw new InvalidOperationException(
                $"The managed business-number definition '{businessKey}' is disabled.");
        }
        if (definition.InitialSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition.InitialSequence),
                "InitialSequence must be greater than zero.");
        }

        var occurredAt = request.OccurredAt ?? DateTimeOffset.UtcNow;
        var scopeKey = NumberFormatRenderer.GetSequenceScope(definition.Format, occurredAt);
        var sequenceKey = CreateSequenceKey(TenantContext.CurrentTenantId, businessKey, scopeKey);

        for (var attempt = 0; attempt < MaxAllocationAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = await database.Queryable<NumberSequenceEntity>()
                .FirstAsync(item => item.SequenceKey == sequenceKey, cancellationToken);

            if (existing is null)
            {
                try
                {
                    var inserted = await database.Insertable(new NumberSequenceEntity(Guid.NewGuid())
                    {
                        TenantId = TenantContext.CurrentTenantId,
                        SequenceKey = sequenceKey,
                        BusinessKey = businessKey,
                        ScopeKey = scopeKey,
                        LastValue = definition.InitialSequence
                    }).ExecuteCommandAsync(cancellationToken);
                    if (inserted > 0)
                    {
                        return NumberFormatRenderer.Render(
                            definition.Format,
                            businessKey,
                            occurredAt,
                            definition.InitialSequence);
                    }
                }
                catch (SqlSugarException) when (attempt < MaxAllocationAttempts - 1)
                {
                    // A competing instance may have created the unique sequence row. Re-read it and allocate normally.
                }

                continue;
            }

            if (existing.LastValue == long.MaxValue)
            {
                throw new OverflowException($"The '{businessKey}' business-number sequence has reached its maximum value.");
            }

            var nextValue = existing.LastValue + 1;
            var updated = await database.Updateable<NumberSequenceEntity>()
                .SetColumns(item => new NumberSequenceEntity { LastValue = nextValue })
                .Where(item => item.Id == existing.Id && item.LastValue == existing.LastValue)
                .ExecuteCommandAsync(cancellationToken);
            if (updated > 0)
            {
                return NumberFormatRenderer.Render(definition.Format, businessKey, occurredAt, nextValue);
            }
        }

        throw new InvalidOperationException(
            $"Unable to allocate a business number for '{businessKey}' after {MaxAllocationAttempts} concurrent attempts.");
    }

    private static string NormalizeBusinessKey(string businessKey)
    {
        if (string.IsNullOrWhiteSpace(businessKey))
        {
            throw new ArgumentException("BusinessKey cannot be empty.", nameof(businessKey));
        }

        var normalized = businessKey.Trim().ToLowerInvariant();
        if (normalized.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(businessKey), "BusinessKey cannot exceed 128 characters.");
        }

        return normalized;
    }

    private static string CreateSequenceKey(Guid? tenantId, string businessKey, string scopeKey)
    {
        var source = $"{tenantId?.ToString("N") ?? "host"}\u001F{businessKey}\u001F{scopeKey}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }
}
