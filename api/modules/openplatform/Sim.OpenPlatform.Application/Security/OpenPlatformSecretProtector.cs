using Microsoft.AspNetCore.DataProtection;
using Sim.OpenPlatform.Application.Contracts;

namespace Sim.OpenPlatform.Application.Security;

/// <summary>
/// 使用 Data Protection 加密 AppSecret；读取配置接口不回显明文。
/// </summary>
public sealed class OpenPlatformSecretProtector(IDataProtectionProvider provider) : IOpenPlatformSecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Sim.OpenPlatform.AppSecret.v1");

    public string Protect(string plainSecret) => _protector.Protect(plainSecret);

    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);
}