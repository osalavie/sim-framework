using Sim.System.Application.Contracts.User;

namespace Sim.System.Application.Security;

public interface IExternalLoginAccountService
{
    Task<LoginUserDto> LoginAsync(string provider, ExternalLoginIdentity identity);

    Task<LoginUserDto> RegisterWithExternalLoginAsync(
        string provider,
        ExternalLoginIdentity identity,
        string userName,
        string password,
        string? nickName,
        string? email,
        string? phone);

    Task<LoginUserDto> LinkWithExternalLoginAsync(
        string provider,
        ExternalLoginIdentity identity,
        string userName,
        string password);
}
