using System.Net.Http;

namespace Sim.Admin.Wpf.Client.Services;

public sealed partial class ApiClient
{
    public Task<NotificationTestResult> TestNotificationProviderAsync(Guid id, string? destination) =>
        SendJsonAsync<NotificationTestResult>(HttpMethod.Post,
            Query($"api/system/notification-administration/{id:D}/test-provider",
                ("destination", destination)));
}

public sealed class NotificationTestResult
{
    public bool Succeeded { get; set; }
    public string? Status { get; set; }
    public string? ErrorCode { get; set; }
}
