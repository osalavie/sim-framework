using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sim.Admin.Wpf.Client.Services;

public sealed partial class ApiClient
{
    public Task<ApprovalCounts> GetApprovalCountsAsync() =>
        SendJsonAsync<ApprovalCounts>(HttpMethod.Get, "api/approval/instance/counts");

    public Task<PagedResult<ApprovalTask>> GetApprovalPendingAsync(int skipCount, int maxResultCount,
        string? keyword = null, string? definitionCode = null) =>
        SendJsonAsync<PagedResult<ApprovalTask>>(HttpMethod.Get, Query("api/approval/instance/my-pending",
            ("skipCount", skipCount.ToString()), ("maxResultCount", maxResultCount.ToString()),
            ("keyword", keyword), ("definitionCode", definitionCode)));

    public Task<PagedResult<ApprovalTask>> GetApprovalHandledAsync(int skipCount, int maxResultCount,
        string? keyword = null, string? definitionCode = null) =>
        SendJsonAsync<PagedResult<ApprovalTask>>(HttpMethod.Get, Query("api/approval/instance/my-handled",
            ("skipCount", skipCount.ToString()), ("maxResultCount", maxResultCount.ToString()),
            ("keyword", keyword), ("definitionCode", definitionCode)));

    public Task<PagedResult<ApprovalInstanceSummary>> GetApprovalSubmittedAsync(int skipCount, int maxResultCount,
        string? keyword = null, string? definitionCode = null) =>
        SendJsonAsync<PagedResult<ApprovalInstanceSummary>>(HttpMethod.Get, Query("api/approval/instance/my-submitted",
            ("skipCount", skipCount.ToString()), ("maxResultCount", maxResultCount.ToString()),
            ("keyword", keyword), ("definitionCode", definitionCode)));

    public Task<PagedResult<ApprovalCc>> GetApprovalCcAsync(int skipCount, int maxResultCount,
        string? keyword = null, string? definitionCode = null) =>
        SendJsonAsync<PagedResult<ApprovalCc>>(HttpMethod.Get, Query("api/approval/instance/my-cc",
            ("skipCount", skipCount.ToString()), ("maxResultCount", maxResultCount.ToString()),
            ("keyword", keyword), ("definitionCode", definitionCode)));

    public async Task MarkApprovalCcReadAsync(Guid ccId)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/approval/instance/mark-cc-read/{ccId:D}");
    }

    public Task<ApprovalInstanceDetail> GetApprovalDetailAsync(Guid instanceId) =>
        SendJsonAsync<ApprovalInstanceDetail>(HttpMethod.Get, $"api/approval/instance/{instanceId:D}");

    public Task<ApprovalInstanceDetail> GetApprovalMonitorDetailAsync(Guid instanceId) =>
        SendJsonAsync<ApprovalInstanceDetail>(HttpMethod.Get, $"api/approval/monitor/{instanceId:D}");

    public Task<PagedResult<ApprovalInstanceSummary>> GetApprovalMonitorAsync(int skipCount, int maxResultCount,
        string? keyword = null, string? status = null, string? businessType = null, string? definitionCode = null) =>
        SendJsonAsync<PagedResult<ApprovalInstanceSummary>>(HttpMethod.Get, Query("api/approval/monitor",
            ("skipCount", skipCount.ToString()), ("maxResultCount", maxResultCount.ToString()),
            ("keyword", keyword), ("status", status), ("businessType", businessType),
            ("definitionCode", definitionCode)));

    public async Task ApproveApprovalTaskAsync(Guid taskId, string? comment)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/approval/instance/approve/{taskId:D}",
            new { comment });
    }

    public async Task RejectApprovalTaskAsync(Guid taskId, string? comment)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/approval/instance/reject/{taskId:D}",
            new { comment });
    }

    public async Task TransferApprovalTaskAsync(Guid taskId, Guid targetUserId, string? comment)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/approval/instance/transfer/{taskId:D}",
            new { targetUserId, comment });
    }

    public async Task AddSignApprovalTaskAsync(Guid taskId, Guid targetUserId, string position, string? comment)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/approval/instance/sign/{taskId:D}",
            new { targetUserId, position, comment });
    }

    public async Task ReturnApprovalToPreviousAsync(Guid taskId, string? comment)
    {
        using var response = await SendAsync(HttpMethod.Post,
            $"api/approval/instance/return-to-previous/{taskId:D}", new { comment });
    }

    public async Task ReturnApprovalToInitiatorAsync(Guid taskId, string? comment)
    {
        using var response = await SendAsync(HttpMethod.Post,
            $"api/approval/instance/return-to-initiator/{taskId:D}", new { comment });
    }

    public async Task WithdrawApprovalAsync(Guid instanceId, string? comment)
    {
        using var response = await SendAsync(HttpMethod.Post,
            $"api/approval/instance/{instanceId:D}/withdraw", new { comment });
    }

    public async Task UrgeApprovalAsync(Guid instanceId)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/approval/instance/{instanceId:D}/urge");
    }

    public async Task TerminateApprovalAsync(Guid instanceId, string comment)
    {
        using var response = await SendAsync(HttpMethod.Post,
            $"api/approval/monitor/{instanceId:D}/terminate", new { comment });
    }

    public async Task ResubmitApprovalAsync(Guid instanceId, string businessType, string businessId,
        string title, IReadOnlyDictionary<string, string> context)
    {
        using var response = await SendAsync(HttpMethod.Post,
            $"api/approval/instance/{instanceId:D}/resubmit",
            new { businessType, businessId, title, context });
    }

    public Task<List<ApprovalUserOption>> GetApprovalUsersAsync(string keyword = "") =>
        SendJsonAsync<List<ApprovalUserOption>>(HttpMethod.Get,
            Query("api/approval/business-type/users", ("keyword", keyword), ("maxResultCount", "20")));

    public Task<List<ApprovalFlowDefinition>> GetApprovalDefinitionsAsync() =>
        SendJsonAsync<List<ApprovalFlowDefinition>>(HttpMethod.Get, "api/approval/flow-definition");

    public Task<ApprovalFlowDefinition> CreateApprovalDefinitionAsync(
        string code, string name, string? businessType, string? description) =>
        SendJsonAsync<ApprovalFlowDefinition>(HttpMethod.Post, "api/approval/flow-definition", new
        {
            code,
            name,
            businessType,
            description,
            nodes = new[]
            {
                new { nodeKey = "__start", name = "开始", nodeType = "Start", approverType = "Initiator" },
                new { nodeKey = "approval1", name = "审批", nodeType = "Approval", approverType = "Initiator" },
                new { nodeKey = "__end", name = "结束", nodeType = "End", approverType = "Initiator" }
            },
            edges = new[]
            {
                new { fromNodeKey = "__start", toNodeKey = "approval1" },
                new { fromNodeKey = "approval1", toNodeKey = "__end" }
            },
            layoutJson = "{\"__start\":{\"x\":260,\"y\":40},\"approval1\":{\"x\":260,\"y\":172},\"__end\":{\"x\":260,\"y\":304}}"
        });

    public Task<ApprovalFlowDefinitionDetail> GetApprovalDefinitionAsync(Guid id) =>
        SendJsonAsync<ApprovalFlowDefinitionDetail>(HttpMethod.Get,
            $"api/approval/flow-definition/{id:D}");

    public Task<ApprovalFlowDefinitionDetail> UpdateApprovalDefinitionAsync(
        ApprovalFlowDefinitionDetail definition) =>
        SendJsonAsync<ApprovalFlowDefinitionDetail>(HttpMethod.Put,
            $"api/approval/flow-definition/{definition.Id:D}", new
            {
                definition.Name,
                definition.Description,
                definition.BusinessType,
                definition.AllowWithdraw,
                definition.WithdrawWindowHours,
                definition.AutoPassWhenInitiator,
                definition.FallbackApproverUserIds,
                definition.Nodes,
                definition.Edges,
                definition.LayoutJson
            });

    public Task<ApprovalFlowValidation> ValidateApprovalDefinitionAsync(Guid id) =>
        SendJsonAsync<ApprovalFlowValidation>(HttpMethod.Post,
            $"api/approval/flow-definition/{id:D}/validate");

    public async Task PublishApprovalDefinitionAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/approval/flow-definition/{id:D}/publish");
    }

    public async Task DisableApprovalDefinitionAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/approval/flow-definition/{id:D}/disable");
    }

    public async Task NewApprovalDefinitionVersionAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/approval/flow-definition/{id:D}/version");
    }

    public async Task DeleteApprovalDefinitionAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/approval/flow-definition/{id:D}");
    }

    public Task UploadApprovalAttachmentAsync(Guid instanceId, string filePath) =>
        UploadFileAsync($"api/approval/instance/{instanceId:D}/upload-attachment", filePath);

    public Task UploadApprovalTaskAttachmentAsync(Guid taskId, string filePath) =>
        UploadFileAsync($"api/approval/instance/upload-task-attachment/{taskId:D}", filePath);

    public Task<byte[]> DownloadApprovalAttachmentAsync(Guid attachmentId) =>
        DownloadBytesAsync($"api/approval/instance/attachment-download/{attachmentId:D}");

    private static string Query(string path, params (string Key, string? Value)[] values)
    {
        var parts = values
            .Where(value => !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => $"{Uri.EscapeDataString(value.Key)}={Uri.EscapeDataString(value.Value!)}")
            .ToList();
        return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
    }

    private async Task<byte[]> DownloadBytesAsync(string path)
    {
        using var response = await SendRawAsync(HttpMethod.Get, path);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task UploadFileAsync(string path, string filePath)
    {
        using var response = await SendRawAsync(HttpMethod.Post, path, () =>
        {
            var content = new MultipartFormDataContent();
            var stream = File.OpenRead(filePath);
            var file = new StreamContent(stream);
            file.Headers.ContentType = new MediaTypeHeaderValue(ContentTypeOf(filePath));
            content.Add(file, "file", Path.GetFileName(filePath));
            return content;
        });
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path,
        Func<HttpContent>? contentFactory = null)
    {
        var tokenBeforeRequest = _accessToken;
        var response = await SendRawOnceAsync(method, path, contentFactory?.Invoke());
        if (response.StatusCode == HttpStatusCode.Unauthorized && tokenBeforeRequest is not null)
        {
            response.Dispose();
            await RefreshAccessTokenAsync(tokenBeforeRequest);
            response = await SendRawOnceAsync(method, path, contentFactory?.Invoke());
        }
        if (response.IsSuccessStatusCode) return response;
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized && tokenBeforeRequest is not null)
            {
                _accessToken = null;
                throw new SessionExpiredException();
            }
            var text = await response.Content.ReadAsStringAsync();
            try
            {
                using var error = JsonDocument.Parse(text);
                if (error.RootElement.TryGetProperty("error", out var detail) &&
                    detail.TryGetProperty("message", out var message) && message.GetString() is { Length: > 0 } value)
                    throw new InvalidOperationException(value);
            }
            catch (JsonException) { }
            throw new HttpRequestException($"API 请求失败（HTTP {(int)response.StatusCode}）。", null, response.StatusCode);
        }
    }

    private async Task<HttpResponseMessage> SendRawOnceAsync(HttpMethod method, string path, HttpContent? content)
    {
        if (_http is null) throw new InvalidOperationException("请先输入 API 地址。");
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (!string.IsNullOrWhiteSpace(_accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await _http.SendAsync(request);
    }

    private static string ContentTypeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".json" => "application/json",
        ".zip" => "application/zip",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };
}

public sealed class ApprovalCounts
{
    public int PendingCount { get; set; }
    public int UnreadCcCount { get; set; }
    public int SubmittedRunningCount { get; set; }
}

public sealed class ApprovalInstanceSummary
{
    public Guid Id { get; set; }
    public string? Number { get; set; }
    public string? DefinitionCode { get; set; }
    public string? DefinitionName { get; set; }
    public int DefinitionVersion { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessId { get; set; }
    public string? Title { get; set; }
    public Guid? InitiatorUserId { get; set; }
    public string? Status { get; set; }
    public string? CurrentNodeKey { get; set; }
    public string? CurrentNodeName { get; set; }
    public DateTime? CreationTime { get; set; }
    public DateTime? CompletedTime { get; set; }
}

public sealed class ApprovalCc
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public string? InstanceNumber { get; set; }
    public string? InstanceTitle { get; set; }
    public string? InstanceStatus { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? CreationTime { get; set; }
    public string ReadState => ReadAt is null ? "未读" : "已读";
}

public sealed class ApprovalInstanceDetail
{
    public Guid Id { get; set; }
    public string? Number { get; set; }
    public string? DefinitionCode { get; set; }
    public string? DefinitionName { get; set; }
    public int DefinitionVersion { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessId { get; set; }
    public string? Title { get; set; }
    public Guid? InitiatorUserId { get; set; }
    public string? Status { get; set; }
    public string? CurrentNodeKey { get; set; }
    public string? CurrentNodeName { get; set; }
    public DateTime? CreationTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public ApprovalContextSnapshot? Context { get; set; }
    public List<ApprovalAttachment> Attachments { get; set; } = [];
    public bool CanWithdraw { get; set; }
    public bool CanResubmit { get; set; }
    public bool CanUploadAttachment { get; set; }
    public List<ApprovalNodeProgress> Nodes { get; set; } = [];
    public List<ApprovalTask> Tasks { get; set; } = [];
    public List<ApprovalRecord> Records { get; set; } = [];
    public List<ApprovalCc> CcRecipients { get; set; } = [];
}

public sealed class ApprovalContextSnapshot
{
    public List<ApprovalContextField> Fields { get; set; } = [];
}

public sealed class ApprovalContextField
{
    public string? Key { get; set; }
    public string? DisplayName { get; set; }
    public string? Type { get; set; }
    public string? Value { get; set; }
}

public sealed class ApprovalNodeProgress
{
    public string? NodeKey { get; set; }
    public string? Name { get; set; }
    public string? NodeType { get; set; }
    public int SortOrder { get; set; }
    public string? Status { get; set; }
    public string? State { get; set; }
    public int Round { get; set; }
    public bool Activated { get; set; }
    public List<ApprovalTask> Tasks { get; set; } = [];
}

public sealed class ApprovalRecord
{
    public Guid Id { get; set; }
    public string? NodeName { get; set; }
    public string? Action { get; set; }
    public Guid? OperatorUserId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string? Comment { get; set; }
    public DateTime? CreationTime { get; set; }
}

public sealed class ApprovalAttachment
{
    public Guid Id { get; set; }
    public string? FileName { get; set; }
    public long Size { get; set; }
    public Guid? UploaderUserId { get; set; }
    public DateTime? CreationTime { get; set; }
    public string SizeText => Size switch
    {
        < 1024 => $"{Size} B",
        < 1024 * 1024 => $"{Size / 1024d:F1} KB",
        _ => $"{Size / 1024d / 1024d:F1} MB"
    };
}

public sealed class ApprovalUserOption
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public string Label => string.IsNullOrWhiteSpace(DisplayName)
        ? UserName ?? Id.ToString()
        : $"{DisplayName}（{UserName}）";
}

public class ApprovalFlowDefinition
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int Version { get; set; }
    public string? Status { get; set; }
    public string? BusinessType { get; set; }
    public int NodeCount { get; set; }
    public DateTime? PublishedTime { get; set; }
    public DateTime? CreationTime { get; set; }
}

public sealed class ApprovalFlowDefinitionDetail : ApprovalFlowDefinition
{
    public bool AllowWithdraw { get; set; } = true;
    public int? WithdrawWindowHours { get; set; }
    public bool AutoPassWhenInitiator { get; set; }
    public List<Guid> FallbackApproverUserIds { get; set; } = [];
    public string? LayoutJson { get; set; }
    public List<ApprovalFlowNode> Nodes { get; set; } = [];
    public List<ApprovalFlowEdge> Edges { get; set; } = [];
}

public sealed class ApprovalFlowNode
{
    public string NodeKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NodeType { get; set; } = "Approval";
    public string ApproverType { get; set; } = "Initiator";
    public List<Guid> ApproverUserIds { get; set; } = [];
    public string? ApproverRoleName { get; set; }
    public Guid? ApproverOrganizationId { get; set; }
    public string MultiApproveMode { get; set; } = "All";
    public string JoinPolicy { get; set; } = "AnyArrival";
    public string? ConditionJson { get; set; }
    public bool AllowTransfer { get; set; } = true;
    public bool AllowAddSign { get; set; } = true;
    public bool AllowReturnToInitiator { get; set; } = true;
    public int? TimeoutHours { get; set; }
    public string? CcConfigJson { get; set; }
}

public sealed class ApprovalFlowEdge
{
    public string FromNodeKey { get; set; } = string.Empty;
    public string ToNodeKey { get; set; } = string.Empty;
    public string? ConditionJson { get; set; }
    public string? Label { get; set; }
}

public sealed class ApprovalFlowValidation
{
    public bool Valid { get; set; }
    public List<string> Problems { get; set; } = [];
}
