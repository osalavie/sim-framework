namespace Sim.System.Application.Contracts.User;

/// <summary>
/// 浏览器会话请求使用的防伪令牌。
/// </summary>
public sealed class AntiforgeryTokenDto
{
    public string RequestToken { get; set; } = string.Empty;
}
