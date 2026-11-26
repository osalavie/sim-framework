using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Application.Contracts.Ports;
using Sim.Approval.Domain.Runtime;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 业务字段声明的运行时助手 —— 统一字段值合并、必填校验与快照构建。
/// 字段形态一律按业务类型声明的 <see cref="ApprovalFieldDto"/> 解释，
/// 未声明的字段按「文本、标签即标识」兜底；快照固化后的展示不再读取当前声明。
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
    /// 校验条件运算符与字段类型是否匹配。字段清单为空或字段未声明时跳过，
    /// 由字段名白名单负责“未声明字段”的拒绝。
    /// </summary>
    public static void EnsureConditionOperatorsMatchFieldTypes(
        IApprovalBusinessDescriptor? descriptor,
        string? conditionJson)
    {
        if (descriptor is null || descriptor.Fields.Count == 0)
        {
            return;
        }

        var declared = descriptor.Fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Key))
            .GroupBy(field => field.Key.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var reference in ApprovalConditionEvaluator.ReferencedFieldOperators(conditionJson))
        {
            if (!declared.TryGetValue(reference.Field, out var field))
            {
                continue;
            }

            var type = ParseFieldType(field.Type);
            if (AllowedConditionOperators.TryGetValue(type, out var operators) &&
                operators.Contains(reference.Operator))
            {
                continue;
            }

            var displayName = field.DisplayName.Length > 0 ? field.DisplayName : field.Key;
            throw new BusinessException(
                ApprovalErrorCodes.ConditionInvalid,
                $"字段 [{displayName}] 的类型为 {type}，不支持条件运算符 [{reference.Operator}]。");
        }
    }

    private static readonly Dictionary<ApprovalFieldType, HashSet<string>> AllowedConditionOperators = new()
    {
        [ApprovalFieldType.String] = Operators(
            "equals", "notEquals", "contains", "notContains", "in", "notIn", "isEmpty", "isNotEmpty"),
        [ApprovalFieldType.Text] = Operators(
            "equals", "notEquals", "contains", "notContains", "in", "notIn", "isEmpty", "isNotEmpty"),
        [ApprovalFieldType.Select] = Operators(
            "equals", "notEquals", "in", "notIn", "isEmpty", "isNotEmpty"),
        [ApprovalFieldType.Number] = Operators(
            "equals", "notEquals", "greaterThan", "greaterOrEqual", "lessThan", "lessOrEqual",
            "isEmpty", "isNotEmpty"),
        [ApprovalFieldType.Decimal] = Operators(
            "equals", "notEquals", "greaterThan", "greaterOrEqual", "lessThan", "lessOrEqual",
            "isEmpty", "isNotEmpty"),
        [ApprovalFieldType.Date] = Operators(
            "equals", "notEquals", "greaterThan", "greaterOrEqual", "lessThan", "lessOrEqual",
            "isEmpty", "isNotEmpty"),
        [ApprovalFieldType.DateTime] = Operators(
            "equals", "notEquals", "greaterThan", "greaterOrEqual", "lessThan", "lessOrEqual",
            "isEmpty", "isNotEmpty"),
        [ApprovalFieldType.Bool] = Operators("equals", "notEquals", "isTrue", "isFalse")
    };

    private static HashSet<string> Operators(params string[] values) =>
        new(values, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 构建业务字段快照：先按业务类型声明的顺序排列（带声明的显示名、类型与枚举选项），
    /// 再追加未声明的字段（标签即标识、按文本处理）。仅保存有取值的字段以控制 <c>ContextJson</c> 体积。
    /// 元数据随实例固化，业务描述符后续变更不影响历史实例展示。
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
                Value = value,
                Options = field.Options
                    .Select(option => new ApprovalFieldOptionDto { Value = option.Value, Label = option.Label })
                    .ToList()
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

    private static bool IsProvided(IReadOnlyDictionary<string, string> values, string key)
    {
        var normalized = (key ?? string.Empty).Trim();
        return normalized.Length > 0
               && values.TryGetValue(normalized, out var value)
               && !string.IsNullOrWhiteSpace(value);
    }
}
