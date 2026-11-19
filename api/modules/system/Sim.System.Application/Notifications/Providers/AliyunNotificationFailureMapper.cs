using Sim.System.Notifications;
using Tea;

namespace Sim.System.Application.Notifications.Providers;

internal static class AliyunNotificationFailureMapper
{
    public static NotificationChannelResult FromService(
        TeaException exception,
        string fallbackCode)
    {
        var transient = exception.StatusCode is 408 or 429 or >= 500 ||
                        exception.Code?.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) == true ||
                        exception.Code?.Contains("INTERNAL", StringComparison.OrdinalIgnoreCase) == true ||
                        exception.Code?.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase) == true ||
                        exception.Code?.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase) == true;
        return new(
            transient
                ? NotificationChannelResultStatus.TransientFailure
                : NotificationChannelResultStatus.PermanentFailure,
            ErrorCode: exception.Code ?? fallbackCode,
            ErrorMessage: exception.Message,
            ConfirmedNotAccepted: true);
    }

    public static NotificationChannelResult FromTransport(
        Exception exception,
        string fallbackCode) => new(
        NotificationChannelResultStatus.Unknown,
        ErrorCode: fallbackCode,
        ErrorMessage: exception.Message);
}
