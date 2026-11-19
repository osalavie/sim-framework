using Microsoft.AspNetCore.DataProtection;

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
        provider.CreateProtector("Sim.System.UserSecuritySecrets.v1");

    public string Protect(byte[] value) =>
        _protector.Protect(Convert.ToBase64String(value));

    public byte[] Unprotect(string protectedValue) =>
        Convert.FromBase64String(_protector.Unprotect(protectedValue));
}
