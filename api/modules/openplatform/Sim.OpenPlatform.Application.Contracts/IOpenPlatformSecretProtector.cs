namespace Sim.OpenPlatform.Application.Contracts;

/// <summary>
/// 开放平台 AppSecret 保护端口（Data Protection）。
/// </summary>
public interface IOpenPlatformSecretProtector
{
    string Protect(string plainSecret);
    string Unprotect(string protectedSecret);
}