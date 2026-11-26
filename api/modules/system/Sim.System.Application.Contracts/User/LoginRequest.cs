using System;

namespace Sim.System.Application.Contracts.User;

public class LoginRequest
{
    public required string UserName { get; set; }
    public required string Password { get; set; }

    /// <summary>
    /// 勾选“记住我”时签发持久 Refresh Cookie；否则签发随浏览器关闭失效的会话 Cookie。
    /// 密码本身始终由浏览器密码库保存，服务端不接收、不存储。
    /// </summary>
    public bool RememberMe { get; set; }

    public string? DeviceId { get; set; }
    public string? DeviceType { get; set; }
    public string? DeviceName { get; set; }
}
