using Microsoft.AspNetCore.DataProtection;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.Security;

public interface IUserSecuritySecretProtector
{
    string Protect(byte[] value);
    byte[] Unprotect(string protectedValue);
}

public sealed class UserSecuritySecretProtector(IDataProtectionProvider provider)
    : IUserSecuritySecretProtector
{
    private readonly IDataProtector _protector =
        provider.CreateProtector(DataProtectionIdentity.UserSecuritySecretsPurpose);

    public string Protect(byte[] value) =>
        _protector.Protect(Convert.ToBase64String(value));

    public byte[] Unprotect(string protectedValue) =>
        Convert.FromBase64String(_protector.Unprotect(protectedValue));
}
