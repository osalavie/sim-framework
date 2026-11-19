using System.Text.Json;
using System.Text.Json.Nodes;
using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Approval.Application.Services;

/// <summary>审批运行时 JSON 序列化助手：业务字段快照、用户列表与抄送配置。</summary>
internal static class ApprovalRuntimeJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string FieldsPropertyName = "fields";

    /// <summary>序列化业务字段快照（无字段返回 null）。</summary>
    public static string? SerializeContextSnapshot(ApprovalContextSnapshotDto snapshot) =>
        snapshot.Fields.Count > 0 ? JsonSerializer.Serialize(snapshot, Options) : null;

    /// <summary>
    /// 反序列化业务字段快照。第一阶段的扁平字典（<c>{"amount":"100"}</c>）按
    /// 「字段标识即标签、类型为文本」映射为字段数组，旧实例照常可读。
    /// </summary>
    public static ApprovalContextSnapshotDto DeserializeContextSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ApprovalContextSnapshotDto();
        }

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
            {
                return new ApprovalContextSnapshotDto();
            }

            if (FindProperty(root, FieldsPropertyName) is JsonArray)
            {
                return JsonSerializer.Deserialize<ApprovalContextSnapshotDto>(json, Options)
                       ?? new ApprovalContextSnapshotDto();
            }

            // 旧格式：扁平字典，键即字段标识。
            return new ApprovalContextSnapshotDto
            {
                Fields = root
                    .Where(entry => entry.Value is not null)
                    .Select(entry => new ApprovalContextFieldDto
                    {
                        Key = entry.Key,
                        DisplayName = entry.Key,
                        Type = nameof(ApprovalFieldType.String),
                        Value = entry.Value!.GetValueKind() == JsonValueKind.String
                            ? entry.Value.GetValue<string>()
                            : entry.Value.ToJsonString()
                    })
                    .ToList()
            };
        }
        catch (JsonException)
        {
            return new ApprovalContextSnapshotDto();
        }
    }

    /// <summary>按属性名取节点值（忽略大小写，兼容不同的序列化命名策略）。</summary>
    private static JsonNode? FindProperty(JsonObject root, string name) =>
        root.FirstOrDefault(entry => string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>把快照摊平为字段值字典，供条件求值使用。</summary>
    public static Dictionary<string, string> ToContextValues(ApprovalContextSnapshotDto snapshot)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in snapshot.Fields)
        {
            if (field.Value is not null)
            {
                values[field.Key] = field.Value;
            }
        }

        return values;
    }

    /// <summary>反序列化业务上下文并摊平为字段值字典（条件求值入口）。</summary>
    public static Dictionary<string, string> DeserializeContextValues(string? json) =>
        ToContextValues(DeserializeContextSnapshot(json));

    /// <summary>序列化用户 ID 列表（空列表返回 null）。</summary>
    public static string? SerializeUserIds(IReadOnlyCollection<Guid>? userIds)
    {
        var distinct = userIds?.Where(id => id != Guid.Empty).Distinct().ToList();
        return distinct is { Count: > 0 } ? JsonSerializer.Serialize(distinct, Options) : null;
    }

    /// <summary>反序列化用户 ID 列表。</summary>
    public static IReadOnlyList<Guid> DeserializeUserIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, Options) ?? [];
        }
        catch (JsonException exception)
        {
            throw new BusinessException(
                ApprovalErrorCodes.ConditionInvalid,
                $"审批人列表 JSON 解析失败: {exception.Message}");
        }
    }

    /// <summary>解析抄送配置。</summary>
    public static ApprovalCcConfiguration DeserializeCcConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ApprovalCcConfiguration();
        }

        try
        {
            return JsonSerializer.Deserialize<ApprovalCcConfiguration>(json, Options)
                   ?? new ApprovalCcConfiguration();
        }
        catch (JsonException exception)
        {
            throw new BusinessException(
                ApprovalErrorCodes.ConditionInvalid,
                $"抄送配置 JSON 解析失败: {exception.Message}");
        }
    }
}

/// <summary>抄送配置：指定用户、角色成员与发起人。</summary>
internal sealed class ApprovalCcConfiguration
{
    /// <summary>抄送用户。</summary>
    public List<Guid> UserIds { get; set; } = [];

    /// <summary>抄送角色（角色代码或名称）。</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>是否抄送发起人。</summary>
    public bool Initiator { get; set; }
}
