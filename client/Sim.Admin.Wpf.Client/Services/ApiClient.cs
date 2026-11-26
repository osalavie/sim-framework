using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sim.Admin.Wpf.Client.Services;

public sealed partial class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private HttpClient? _http;
    private string? _accessToken;

    public Guid? CurrentUserId { get; private set; }

    public void Connect(string address)
    {
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("请输入有效的 HTTP(S) API 根地址。");

        // Changing servers must discard both credentials and cookies.
        _http?.Dispose();
        _http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true })
        {
            BaseAddress = new Uri(uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(20)
        };
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-Hans");
        _accessToken = null;
        CurrentUserId = null;
    }

    public Task<LoginResult> LoginAsync(string userName, string password, bool rememberMe = false) =>
        SendJsonAsync<LoginResult>(HttpMethod.Post, "api/system/user/login",
            new { userName, password, rememberMe, deviceType = "PC", deviceName = "Sim Admin WPF Client" });

    public async Task SendTwoFactorCodeAsync(Guid challengeId, string method)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/system/user/send-two-factor-code", new { challengeId, method });
    }

    public Task<LoginResult> CompleteTwoFactorAsync(
        Guid challengeId, string method, string code, bool rememberMe = false) =>
        SendJsonAsync<LoginResult>(HttpMethod.Post, "api/system/user/complete-two-factor-login",
            new { challengeId, method, code, rememberMe, deviceType = "PC", deviceName = "Sim Admin WPF Client" });

    public Task<UserProfile> GetMyProfileAsync() =>
        SendJsonAsync<UserProfile>(HttpMethod.Get, "api/system/user/my-profile");

    public Task<UserProfile> UpdateMyProfileAsync(string nickName, string? name, string? description) =>
        SendJsonAsync<UserProfile>(HttpMethod.Put, "api/system/user/my-profile",
            new { nickName, name, description });

    public async Task<string?> UploadMyAvatarAsync(byte[] png)
    {
        using var response = await SendRawAsync(HttpMethod.Post, "api/system/user/upload-my-avatar", () =>
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(png);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "avatar.png");
            return form;
        });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("avatar", out var avatar) ? avatar.GetString() : null;
    }

    public Task<UserNotificationSummary> GetNotificationsAsync() =>
        SendJsonAsync<UserNotificationSummary>(HttpMethod.Get, "api/system/user-notification?maxCount=20");

    public async Task MarkNotificationReadAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/system/user-notification/{id:D}/mark-read");
    }

    public async Task MarkAllNotificationsReadAsync()
    {
        using var response = await SendAsync(HttpMethod.Post, "api/system/user-notification/mark-all-read");
    }

    public Task<PagedResult<ApprovalTask>> GetMyPendingAsync() =>
        SendJsonAsync<PagedResult<ApprovalTask>>(HttpMethod.Get,
            "api/approval/instance/my-pending?skipCount=0&maxResultCount=20");

    // Shared transport for desktop administration pages. Paths come from the local
    // module catalog; the server remains authoritative for access control.
    public async Task<JsonElement> GetDocumentAsync(string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    public async Task SendDocumentAsync(HttpMethod method, string path, object? body = null)
    {
        using var response = await SendAsync(method, path, body);
    }

    public async Task DecideApprovalAsync(Guid taskId, bool approve, string? comment)
    {
        var action = approve ? "approve" : "reject";
        using var response = await SendAsync(HttpMethod.Post, $"api/approval/instance/{action}/{taskId:D}",
            new { comment });
    }

    public async Task<UserPreference?> GetMyPreferencesAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, "api/system/user-preference/my-preferences");
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<UserPreference>(JsonOptions);
    }

    public Task<UserPreference> UpdateMyPreferencesAsync(string style, bool dark) =>
        SendJsonAsync<UserPreference>(HttpMethod.Put, "api/system/user-preference/my-preferences",
            new { themeStyle = style, themeMode = dark ? "dark" : "light" });

    public async Task ChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/system/user/change-password",
            new { currentPassword, newPassword, confirmPassword });
    }

    public Task<TwoFactorStatus> GetTwoFactorStatusAsync() =>
        SendJsonAsync<TwoFactorStatus>(HttpMethod.Get, "api/system/user/two-factor-status");

    public Task<AuthenticatorSetup> BeginAuthenticatorSetupAsync(string currentPassword) =>
        SendJsonAsync<AuthenticatorSetup>(HttpMethod.Post, "api/system/user/begin-authenticator-setup",
            new { currentPassword });

    public Task<AuthenticatorRecoveryCodes> ConfirmAuthenticatorSetupAsync(string code) =>
        SendJsonAsync<AuthenticatorRecoveryCodes>(HttpMethod.Post, "api/system/user/confirm-authenticator-setup",
            new { code });

    public Task<PasswordResetChallenge> RequestMessageTwoFactorSetupAsync(string method, string currentPassword) =>
        SendJsonAsync<PasswordResetChallenge>(HttpMethod.Post, "api/system/user/request-message-two-factor-setup",
            new { method, currentPassword });

    public async Task ConfirmMessageTwoFactorSetupAsync(Guid challengeId, string code)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/system/user/confirm-message-two-factor-setup",
            new { challengeId, code });
    }

    public async Task DisableTwoFactorAsync(string method, string currentPassword)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/system/user/disable-two-factor",
            new { method, currentPassword });
    }

    public Task<List<LoginSession>> GetMyLoginSessionsAsync() =>
        SendJsonAsync<List<LoginSession>>(HttpMethod.Get, "api/system/user/my-login-sessions");

    public async Task RevokeMyLoginSessionAsync(Guid sessionId)
    {
        using var response = await SendAsync(HttpMethod.Post,
            $"api/system/user/revoke-my-login-session/{sessionId:D}");
    }

    public async Task DeleteMyAccountAsync(string password)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/system/user/delete-my-account",
            new { password });
    }

    public void AcceptSession(LoginResult result)
    {
        if (string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.UserName))
            throw new InvalidOperationException("API 未返回有效登录会话。");
        _accessToken = result.AccessToken;
        CurrentUserId = result.Id;
    }

    public async Task LogoutAsync()
    {
        var baseAddress = _http?.BaseAddress;
        try
        {
            var token = await SendJsonAsync<AntiforgeryToken>(HttpMethod.Get, "api/system/user/antiforgery-token", retry: false);
            using var response = await SendAsync(HttpMethod.Post, "api/system/user/revoke-refresh-token", null, token.RequestToken, retry: false);
        }
        finally
        {
            _accessToken = null;
            CurrentUserId = null;
            // A failed revoke cannot leave the cookie usable in this process.
            if (baseAddress is not null) Connect(baseAddress.ToString());
        }
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string path, object? body = null, bool retry = true, string? xsrfToken = null)
    {
        using var response = await SendAsync(method, path, body, xsrfToken, retry);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions) ??
            throw new InvalidOperationException("API 返回了空响应。");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? xsrfToken = null, bool retry = true)
    {
        var tokenBeforeRequest = _accessToken;
        var response = await SendOnceAsync(method, path, body, xsrfToken);
        if (retry && response.StatusCode == HttpStatusCode.Unauthorized && tokenBeforeRequest is not null)
        {
            response.Dispose();
            await RefreshAccessTokenAsync(tokenBeforeRequest);
            response = await SendOnceAsync(method, path, body, xsrfToken);
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

    private async Task RefreshAccessTokenAsync(string expiredToken)
    {
        await _refreshLock.WaitAsync();
        try
        {
            if (_accessToken != expiredToken) return;
            var token = await SendJsonAsync<AntiforgeryToken>(HttpMethod.Get,
                "api/system/user/antiforgery-token", retry: false);
            var session = await SendJsonAsync<LoginResult>(HttpMethod.Post,
                "api/system/user/refresh-token", retry: false, xsrfToken: token.RequestToken);
            AcceptSession(session);
        }
        catch
        {
            _accessToken = null;
            throw new SessionExpiredException();
        }
        finally { _refreshLock.Release(); }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, object? body, string? xsrfToken)
    {
        if (_http is null) throw new InvalidOperationException("请先输入 API 地址。");
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        if (!string.IsNullOrWhiteSpace(_accessToken))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        if (!string.IsNullOrWhiteSpace(xsrfToken)) request.Headers.Add("X-XSRF-TOKEN", xsrfToken);
        return await _http.SendAsync(request);
    }

    public void Dispose()
    {
        _http?.Dispose();
        _refreshLock.Dispose();
    }
}

public sealed class SessionExpiredException() : InvalidOperationException("登录已失效，请重新登录。");

public sealed class LoginResult
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public string? AccessToken { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public Guid? TwoFactorChallengeId { get; set; }
    public List<string> TwoFactorMethods { get; set; } = [];
    public List<RoleInfo>? Roles { get; set; }
}

public sealed class RoleInfo
{
    public string? Name { get; set; }
}

public sealed class AntiforgeryToken
{
    public string RequestToken { get; set; } = string.Empty;
}

public sealed class UserProfile
{
    public string UserName { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Avatar { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Description { get; set; }
    public List<RoleInfo>? Roles { get; set; }
}

public sealed class UserNotificationSummary
{
    public int UnreadCount { get; set; }
    public List<UserNotification> Items { get; set; } = [];
}

public sealed class UserNotification
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreationTime { get; set; }
}

public sealed class PagedResult<T>
{
    public int TotalCount { get; set; }
    public List<T> Items { get; set; } = [];
}

public sealed class ApprovalTask
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public string? NodeKey { get; set; }
    public string? NodeName { get; set; }
    public string? TaskType { get; set; }
    public string? Status { get; set; }
    public string? InstanceNumber { get; set; }
    public string? InstanceTitle { get; set; }
    public string? InstanceStatus { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessId { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public string? Comment { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? HandledTime { get; set; }
    public DateTime? CreationTime { get; set; }
    public ApprovalTaskPermissions? Permissions { get; set; }
}

public sealed class ApprovalTaskPermissions
{
    public bool CanApprove { get; set; }
    public bool CanTransfer { get; set; }
    public bool CanAddSign { get; set; }
    public bool CanReturnToPrevious { get; set; }
    public bool CanReturnToInitiator { get; set; }
}

public sealed class UserPreference
{
    public string? ThemeStyle { get; set; }
    public string? ThemeMode { get; set; }
}

public sealed class TwoFactorStatus
{
    public bool AuthenticatorEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public bool SmsEnabled { get; set; }
    public string? MaskedEmail { get; set; }
    public string? MaskedPhone { get; set; }
    public int RecoveryCodesRemaining { get; set; }
}

public sealed class AuthenticatorSetup
{
    public string? SharedKey { get; set; }
    public string? AuthenticatorUri { get; set; }
    public int ExpiresInSeconds { get; set; }
}

public sealed class AuthenticatorRecoveryCodes
{
    public List<string> RecoveryCodes { get; set; } = [];
}

public sealed class PasswordResetChallenge
{
    public Guid? ChallengeId { get; set; }
}

public sealed class LoginSession
{
    public Guid Id { get; set; }
    public string? DeviceType { get; set; }
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public DateTime LoginAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsOnline { get; set; }
}
