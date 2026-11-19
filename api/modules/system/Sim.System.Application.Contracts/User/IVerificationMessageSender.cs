namespace Sim.System.Application.Contracts.User;

public interface IVerificationMessageSender
{
    Task<bool> SendAsync(
        string method,
        string destination,
        string code,
        string purpose,
        CancellationToken cancellationToken = default);
}
