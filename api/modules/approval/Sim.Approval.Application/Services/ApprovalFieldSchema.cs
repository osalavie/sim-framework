using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Application.Contracts.Ports;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 业务字段声明的运行时助手 —— 统一字段值合并、必填校验、快照构建与读取时元数据补齐。
/// 字段形态一律按业务类型声明的 <see cref="ApprovalFieldDto"/> 解释，
/// 未声明的字段按「文本、标签即标识」兜底，保证旧实例与未接入业务类型照常可读。
/// </summary>
internal static class ApprovalFieldSchema
{
    /// <summary>解析字段类型，无法识别时回退为文本。</summary>
    public static ApprovalFieldType ParseFieldType(string? value) =>
        Enum.TryParse<ApprovalFieldType>(value?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : ApprovalFieldType.String;

    /// <summary>
    /// 合并字段值：<c>Fields</c> 优先于 <c>Context</c>（同键覆盖），
    /// 字段标识为空或取值缺失的条目被忽略。
    /// </summary>
    public static Dictionary<string, string> MergeValues(
        IReadOnlyDictionary<string, string>? context,
        IReadOnlyList<ApprovalFieldValueDto>? fields)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context is not null)
        {
            foreach (var entry in context)
            {
                var key = (entry.Key ?? string.Empty).Trim();
                if (key.Length > 0 && entry.Value is not null)
                {
                    values[key] = entry.Value;
                }
            }
        }

        if (fields is not null)
        {
            foreach (var field in fields)
            {
                var key = (field.Key ?? string.Empty).Trim();
                if (key.Length > 0 && field.Value is not null)
                {
                    values[key] = field.Value;
                }
            }
        }

        return values;
    }

    /// <summary>校验业务类型声明的必填字段；缺失或为空时拒绝提交。</summary>
    public static void EnsureRequiredFields(
        IApprovalBusinessDescriptor? descriptor,
        IReadOnlyDictionary<string, string> values)
    {
        if (descriptor is null)
        {
            return;
        }

        var missing = descriptor.Fields
            .Where(field => field.Required && !IsProvided(values, field.Key))
            .Select(field => field.DisplayName.Length > 0 ? field.DisplayName : field.Key)
            .ToList();
        if (missing.Count > 0)
        {
            throw new BusinessException(
                ApprovalErrorCodes.InvalidRequest,
                $"业务类型 [{descriptor.BusinessType}] 的必填字段 [{string.Join(", ", missing)}] 未提供。");
        }
    }

    /// <summary>
    /// 构建业务字段快照：先按业务类型声明的顺序排列（带声明的显示名与类型），
    /// 再追加未声明的字段（标签即标识、按文本处理）。仅保存有取值的字段以控制 <c>ContextJson</c> 体积。
    /// </summary>
    public static ApprovalContextSnapshotDto BuildSnapshot(
        IReadOnlyList<ApprovalFieldDto>? declared,
        IReadOnlyDictionary<string, string> values)
    {
        var snapshot = new ApprovalContextSnapshotDto();
        var consumed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in declared ?? [])
        {
            var key = (field.Key ?? string.Empty).Trim();
            if (key.Length == 0 || !consumed.Add(key) || !values.TryGetValue(key, out var value))
            {
                continue;
            }

            snapshot.Fields.Add(new ApprovalContextFieldDto
            {
                Key = key,
                DisplayName = field.DisplayName.Length > 0 ? field.DisplayName : key,
                Type = field.Type.Length > 0 ? field.Type : nameof(ApprovalFieldType.String),
                Value = value
            });
        }

        foreach (var entry in values.Where(item => !consumed.Contains(item.Key)).OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            snapshot.Fields.Add(new ApprovalContextFieldDto
            {
                Key = entry.Key,
                DisplayName = entry.Key,
                Type = nameof(ApprovalFieldType.String),
                Value = entry.Value
            });
        }

        return snapshot;
    }

    /// <summary>
    /// 按当前业务类型声明补齐快照的显示名、类型与枚举选项（读取时调用，不落库）。
    /// 快照自身保存的显示名作为兜底，使业务类型后续改名不影响历史实例的可读性。
    /// </summary>
    public static ApprovalContextSnapshotDto ApplyDeclaredMetadata(
        ApprovalContextSnapshotDto snapshot,
        IReadOnlyList<ApprovalFieldDto>? declared)
    {
        if (declared is null || declared.Count == 0)
        {
            return snapshot;
        }

        var byKey = declared
            .Where(field => !string.IsNullOrWhiteSpace(field.Key))
            .GroupBy(field => field.Key.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var field in snapshot.Fields)
        {
            if (!byKey.TryGetValue(field.Key, out var declaration))
            {
                continue;
            }

            field.DisplayName = declaration.DisplayName.Length > 0 ? declaration.DisplayName : field.DisplayName;
            field.Type = declaration.Type.Length > 0 ? declaration.Type : field.Type;
            field.Options = declaration.Options
                .Select(option => new ApprovalFieldOptionDto { Value = option.Value, Label = option.Label })
                .ToList();
        }

        return snapshot;
    }

    private static bool IsProvided(IReadOnlyDictionary<string, string> values, string key)
    {
        var normalized = (key ?? string.Empty).Trim();
        return normalized.Length > 0
               && values.TryGetValue(normalized, out var value)
               && !string.IsNullOrWhiteSpace(value);
    }
}
