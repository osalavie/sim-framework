using Sim.Approval.Domain.Runtime;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Security.Tests;

/// <summary>
/// 审批节点条件求值器测试：覆盖匹配模式、比较语义与结构校验。
/// </summary>
public class ApprovalConditionEvaluatorTests
{
    private static readonly Dictionary<string, string> Context = new(StringComparer.Ordinal)
    {
        ["amount"] = "1500",
        ["urgent"] = "true",
        ["level"] = "L2",
        ["applicant"] = "张三"
    };

    [Fact]
    public void Empty_condition_should_activate_node()
    {
        Assert.True(ApprovalConditionEvaluator.IsActive(null, Context));
        Assert.True(ApprovalConditionEvaluator.IsActive("  ", Context));
        Assert.True(ApprovalConditionEvaluator.IsActive("{\"rules\":[]}", Context));
    }

    [Fact]
    public void All_match_should_require_every_rule_to_pass()
    {
        const string matched = """
            {
              "match": "all",
              "rules": [
                { "field": "amount", "operator": "greaterOrEqual", "value": "1000" },
                { "field": "urgent", "operator": "isTrue" }
              ]
            }
            """;
        const string unmatched = """
            {
              "match": "all",
              "rules": [
                { "field": "amount", "operator": "greaterOrEqual", "value": "2000" },
                { "field": "urgent", "operator": "isTrue" }
              ]
            }
            """;

        Assert.True(ApprovalConditionEvaluator.IsActive(matched, Context));
        Assert.False(ApprovalConditionEvaluator.IsActive(unmatched, Context));
    }

    [Fact]
    public void Any_match_should_pass_when_one_rule_passes()
    {
        const string condition = """
            {
              "match": "any",
              "rules": [
                { "field": "amount", "operator": "greaterThan", "value": "5000" },
                { "field": "level", "operator": "in", "value": "L1,L2" }
              ]
            }
            """;

        Assert.True(ApprovalConditionEvaluator.IsActive(condition, Context));
    }

    [Theory]
    [InlineData("equals", "1500", true)]
    [InlineData("notEquals", "1500", false)]
    [InlineData("lessOrEqual", "1500", true)]
    [InlineData("contains", "50", true)]
    [InlineData("notContains", "50", false)]
    public void Comparison_operators_should_follow_numeric_and_text_semantics(
        string @operator,
        string value,
        bool expected)
    {
        var condition = $$"""
            { "rules": [ { "field": "amount", "operator": "{{@operator}}", "value": "{{value}}" } ] }
            """;

        Assert.Equal(expected, ApprovalConditionEvaluator.IsActive(condition, Context));
    }

    [Fact]
    public void Numeric_comparison_should_not_fall_back_to_ordinal_text()
    {
        // "900" 作为文本大于 "1500"，作为数值小于；必须按数值比较。
        const string condition = """
            { "rules": [ { "field": "amount", "operator": "greaterThan", "value": "900" } ] }
            """;

        Assert.True(ApprovalConditionEvaluator.IsActive(condition, Context));
    }

    [Fact]
    public void Missing_field_should_be_treated_as_empty()
    {
        const string isEmpty = """
            { "rules": [ { "field": "unknown", "operator": "isEmpty" } ] }
            """;

        Assert.True(ApprovalConditionEvaluator.IsActive(isEmpty, Context));
    }

    [Fact]
    public void Referenced_fields_should_be_distinct_and_ignore_blanks()
    {
        const string condition = """
            {
              "rules": [
                { "field": "amount", "operator": "greaterThan", "value": "1" },
                { "field": "amount", "operator": "lessThan", "value": "9" },
                { "field": "level", "operator": "isNotEmpty" }
              ]
            }
            """;

        var fields = ApprovalConditionEvaluator.ReferencedFields(condition);
        Assert.Equal(["amount", "level"], fields);
    }

    [Fact]
    public void Validate_should_reject_unsupported_operator_and_missing_field()
    {
        var unsupported = Assert.Throws<BusinessException>(() =>
            ApprovalConditionEvaluator.Validate(
                """{ "rules": [ { "field": "amount", "operator": "matches", "value": "1" } ] }"""));
        Assert.Equal(ApprovalErrorCodes.ConditionInvalid, unsupported.Code);

        var missingField = Assert.Throws<BusinessException>(() =>
            ApprovalConditionEvaluator.Validate("""{ "rules": [ { "operator": "equals", "value": "1" } ] }"""));
        Assert.Equal(ApprovalErrorCodes.ConditionInvalid, missingField.Code);
    }

    [Fact]
    public void Validate_should_accept_supported_operators_and_empty_condition()
    {
        ApprovalConditionEvaluator.Validate(null);
        ApprovalConditionEvaluator.Validate("{}");
        ApprovalConditionEvaluator.Validate(
            """{ "match": "any", "rules": [ { "field": "amount", "operator": "greaterOrEqual", "value": "100" } ] }""");
    }
}
