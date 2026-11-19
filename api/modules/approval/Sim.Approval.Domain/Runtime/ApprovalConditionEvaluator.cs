using System.Globalization;
using System.Text.Json;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Approval.Domain.Runtime;

/// <summary>
/// 节点激活条件求值器。条件以 JSON 描述，不使用动态表达式，保证可校验与可预期：
/// <code>
/// {
///   "match": "all",
///   "rules": [
///     { "field": "amount", "operator": "greaterOrEqual", "value": "1000" },
///     { "field": "urgent", "operator": "isTrue" }
///   ]
/// }
/// </code>
/// <c>match</c> 取 all（默认）或 any；条件为空表示节点无条件激活。
/// <c>value</c> 一律为字符串，比较时按数值、时间、文本依次尝试。
/// </summary>
public static class ApprovalConditionEvaluator
{
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "equals", "notEquals", "greaterThan", "greaterOrEqual", "lessThan", "lessOrEqual",
        "contains", "notContains", "in", "notIn", "isEmpty", "isNotEmpty", "isTrue", "isFalse"
    };

    /// <summary>节点是否满足激活条件（条件为空视为无条件激活）。</summary>
    public static bool IsActive(string? conditionJson, IReadOnlyDictionary<string, string> context)
    {
        if (string.IsNullOrWhiteSpace(conditionJson))
        {
            return true;
        }

        var condition = Parse(conditionJson);
        if (condition.Rules.Count == 0)
        {
            return true;
        }

        var results = condition.Rules.Select(rule => EvaluateRule(rule, context));
        return condition.MatchAny ? results.Any(result => result) : results.All(result => result);
    }

    /// <summary>校验条件 JSON 结构，非法时抛出 <see cref="ApprovalErrorCodes.ConditionInvalid"/>。</summary>
    public static void Validate(string? conditionJson)
    {
        if (string.IsNullOrWhiteSpace(conditionJson))
        {
            return;
        }

        var condition = Parse(conditionJson);
        foreach (var rule in condition.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Field))
            {
                throw InvalidCondition("条件规则缺少 field。");
            }

            if (string.IsNullOrWhiteSpace(rule.Operator) || !SupportedOperators.Contains(rule.Operator))
            {
                throw InvalidCondition($"不支持的条件运算符: {rule.Operator ?? "(空)"}");
            }
        }
    }

    /// <summary>提取条件中引用的字段名（用于比对业务描述符声明的可条件字段）。</summary>
    public static IReadOnlyList<string> ReferencedFields(string? conditionJson)
    {
        if (string.IsNullOrWhiteSpace(conditionJson))
        {
            return [];
        }

        return Parse(conditionJson).Rules
            .Select(rule => rule.Field)
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool EvaluateRule(ConditionRule rule, IReadOnlyDictionary<string, string> context)
    {
        context.TryGetValue(rule.Field ?? string.Empty, out var actual);
        var expected = rule.Value;
        var operatorName = (rule.Operator ?? string.Empty).ToLowerInvariant();

        switch (operatorName)
        {
            case "equals":
                return CompareValues(actual, expected) == 0;
            case "notequals":
                return CompareValues(actual, expected) != 0;
            case "greaterthan":
                return CompareValues(actual, expected) > 0;
            case "greaterorequal":
                return CompareValues(actual, expected) >= 0;
            case "lessthan":
                return CompareValues(actual, expected) < 0;
            case "lessorequal":
                return CompareValues(actual, expected) <= 0;
            case "contains":
                return actual is not null && expected is not null &&
                       actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
            case "notcontains":
                return actual is null || expected is null ||
                       !actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
            case "in":
                return expected is not null && expected
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(item => string.Equals(item, actual, StringComparison.OrdinalIgnoreCase));
            case "notin":
                return expected is null || !expected
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(item => string.Equals(item, actual, StringComparison.OrdinalIgnoreCase));
            case "isempty":
                return string.IsNullOrWhiteSpace(actual);
            case "isnotempty":
                return !string.IsNullOrWhiteSpace(actual);
            case "istrue":
                return bool.TryParse(actual, out var trueValue) && trueValue;
            case "isfalse":
                return actual is null || (bool.TryParse(actual, out var falseValue) && !falseValue);
            default:
                throw InvalidCondition($"不支持的条件运算符: {rule.Operator}");
        }
    }

    /// <summary>按数值、日期时间、文本依次尝试比较；无法解析为前两者时做序号文本比较。</summary>
    private static int CompareValues(string? actual, string? expected)
    {
        if (actual is null || expected is null)
        {
            return string.CompareOrdinal(actual, expected);
        }

        if (decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var actualNumber) &&
            decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var expectedNumber))
        {
            return actualNumber.CompareTo(expectedNumber);
        }

        if (DateTime.TryParse(actual, CultureInfo.InvariantCulture, DateTimeStyles.None, out var actualTime) &&
            DateTime.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expectedTime))
        {
            return actualTime.CompareTo(expectedTime);
        }

        return string.CompareOrdinal(actual, expected);
    }

    private static ConditionDefinition Parse(string conditionJson)
    {
        ConditionDefinition? condition;
        try
        {
            condition = JsonSerializer.Deserialize<ConditionDefinition>(conditionJson, ConditionJsonOptions);
        }
        catch (JsonException exception)
        {
            throw InvalidCondition($"条件 JSON 解析失败: {exception.Message}");
        }

        if (condition is null)
        {
            throw InvalidCondition("条件 JSON 为空。");
        }

        return condition;
    }

    private static BusinessException InvalidCondition(string message) =>
        new(ApprovalErrorCodes.ConditionInvalid, message);

    private static readonly JsonSerializerOptions ConditionJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class ConditionDefinition
    {
        public string? Match { get; set; }

        public List<ConditionRule> Rules { get; set; } = [];

        public bool MatchAny => string.Equals(Match, "any", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ConditionRule
    {
        public string? Field { get; set; }

        public string? Operator { get; set; }

        public string? Value { get; set; }
    }
}
