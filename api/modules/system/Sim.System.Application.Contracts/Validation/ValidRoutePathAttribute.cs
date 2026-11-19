using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Sim.System.Application.Contracts.Validation;

/// <summary>
/// Validates that a route path follows the correct format
/// - Optional leading /
/// - Supports static segments, :param, :param?, :param(regex) and * wildcards
/// - Can contain alphanumeric characters, hyphens, underscores, and forward slashes
/// - Cannot contain consecutive slashes
/// - Cannot end with a slash (except for root path "/")
/// </summary>
public class ValidRoutePathAttribute : ValidationAttribute
{
    private static readonly Regex RoutePathRegex = new(
        @"^/?(?:[a-zA-Z0-9_-]+|:[a-zA-Z0-9_]+(?:\([^)]*\))?\??\*?|\*)(?:/(?:[a-zA-Z0-9_-]+|:[a-zA-Z0-9_]+(?:\([^)]*\))?\??\*?|\*))*$",
        RegexOptions.Compiled
    );

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success; // Null/empty is handled by [Required] attribute
        }

        var path = value.ToString()!;

        // Allow root path
        if (path == "/")
        {
            return ValidationResult.Success;
        }

        // Validate format
        if (!RoutePathRegex.IsMatch(path))
        {
            var message = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
                ? $"{validationContext.DisplayName} 只能包含字母、数字、连字符、下划线、正斜杠，并可使用 :param、:param? 等动态段；不能包含连续斜杠或以斜杠结尾。"
                : $"{validationContext.DisplayName} may contain only letters, numbers, hyphens, underscores, forward slashes, and dynamic segments such as :param or :param?; consecutive or trailing slashes are not allowed.";
            return new ValidationResult(ErrorMessage ?? message);
        }

        return ValidationResult.Success;
    }
}
