using Sim.Framework.Application.Contracts.IServices;

namespace Sim.Framework.Core.Conventions;

/// <summary>
/// 应用服务类型判定与 HTTP 动词/路由名约定工具。
/// 路由规则与 ABP 常规控制器（ConventionalRouteBuilder + HttpMethodHelper）保持一致，
/// 以保证既有前端 API 契约不变。
/// </summary>
internal static class AppServiceTypeHelper
{
    /// <summary>
    /// 判断类型是否为需要暴露为 API 控制器的应用服务。
    /// </summary>
    public static bool IsAppService(Type type)
    {
        if (!type.IsClass || type.IsAbstract || !type.IsPublic || type.IsGenericTypeDefinition)
        {
            return false;
        }

        return typeof(IApplicationService).IsAssignableFrom(type);
    }

    // 与 ABP HttpMethodHelper.ConventionalPrefixes 保持一致
    private static readonly (string Verb, string[] Prefixes)[] ConventionalPrefixes =
    [
        ("GET", ["GetList", "GetAll", "Get"]),
        ("PUT", ["Put", "Update"]),
        ("DELETE", ["Delete", "Remove"]),
        ("POST", ["Create", "Add", "Insert", "Post"]),
        ("PATCH", ["Patch"])
    ];

    private const string DefaultVerb = "POST";

    /// <summary>按方法名前缀推断 HTTP 动词，默认 POST。</summary>
    public static string GetConventionalVerb(string methodName)
    {
        foreach (var (verb, prefixes) in ConventionalPrefixes)
        {
            if (prefixes.Any(p => methodName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                return verb;
            }
        }

        return DefaultVerb;
    }

    /// <summary>按推断出的动词移除方法名前缀。</summary>
    public static string RemoveVerbPrefix(string methodName, string verb)
    {
        var prefixes = ConventionalPrefixes.FirstOrDefault(p => p.Verb == verb).Prefixes;
        if (prefixes == null)
        {
            return methodName;
        }

        var prefix = prefixes.FirstOrDefault(p => methodName.StartsWith(p, StringComparison.Ordinal));
        return prefix == null ? methodName : methodName[prefix.Length..];
    }

    /// <summary>移除第一个匹配的后缀（与 ABP RemovePostFix 行为一致）。</summary>
    public static string RemovePostFix(string value, params string[] postfixes)
    {
        foreach (var postfix in postfixes)
        {
            if (value.EndsWith(postfix, StringComparison.Ordinal))
            {
                return value[..^postfix.Length];
            }
        }

        return value;
    }

    /// <summary>PascalCase 转 kebab-case（与 ABP ToKebabCase 行为一致）。</summary>
    public static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Length == 1)
        {
            return value.ToLowerInvariant();
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == ' ')
            {
                builder.Append('-');
                continue;
            }

            if (char.IsUpper(c))
            {
                if (i > 0 && builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>基元扩展类型判定（与 ABP TypeHelper.IsPrimitiveExtended 一致）。</summary>
    public static bool IsPrimitiveExtended(Type type, bool includeEnums = true)
    {
        if (type.IsPrimitive)
        {
            return true;
        }

        if (includeEnums && type.IsEnum)
        {
            return true;
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return IsPrimitiveExtended(underlying, includeEnums);
        }

        return type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateTime)
               || type == typeof(DateTimeOffset)
               || type == typeof(TimeSpan)
               || type == typeof(Guid);
    }
}
