using System.Text.RegularExpressions;

namespace Sim.Admin.Wpf.Client.Features.Common;

/// <summary>表单基础格式校验，与 Web 管理端的规则保持一致。</summary>
public static class FieldValidation
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^1[3-9]\d{9}$", RegexOptions.Compiled);

    public static string? UserName(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        return value.Length is > 0 and (< 3 or > 20) ? "账号长度须为 3–20 个字符。" : null;
    }

    public static string? NickName(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        return value.Length > 20 ? "昵称不得超过 20 个字符。" : null;
    }

    public static string? Email(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        return value.Length > 0 && !EmailPattern.IsMatch(value) ? "邮箱格式不正确。" : null;
    }

    public static string? Phone(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        return value.Length > 0 && !PhonePattern.IsMatch(value) ? "手机号格式不正确。" : null;
    }
}
