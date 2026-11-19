using Sim.System.Application.Contracts.User;
using Sim.System.Application.Contracts.Notifications;

namespace Sim.System.Application.Security;

/// <summary>
/// 迁移期桥接器。RBAC 只表达安全验证码意图，渠道与供应商由 System 通知平台选择。
/// </summary>
public sealed class VerificationMessageSender(
    ISecurityNotificationSender notificationSender) : IVerificationMessageSender
{
    public async Task<bool> SendAsync(
        string method,
        string destination,
        string code,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        var channel = method switch
        {
            TwoFactorMethods.Email => "email",
            TwoFactorMethods.Sms => "sms",
            _ => null
        };
        if (channel == null)
        {
            return false;
        }

        var result = await notificationSender.SendAsync(
            new SecurityNotificationCommand
            {
                Channel = channel,
                Destination = destination,
                Code = code,
                Purpose = purpose
            },
            cancellationToken);
        return result.Succeeded;
    }
}
