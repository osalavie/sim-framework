using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.Contracts.Validation;

/// <summary>
/// Validates that a layout type is one of the allowed values: admin, custom, none
/// </summary>
public class ValidLayoutTypeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success; // Null/empty is handled by [Required] attribute
        }

        var layoutType = value.ToString()!;

        if (!SystemConsts.LayoutTypes.IsValid(layoutType))
        {
            var validTypes = string.Join(", ", SystemConsts.LayoutTypes.ValidTypes);
            var message = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
                ? $"{validationContext.DisplayName} 必须是以下值之一：{validTypes}"
                : $"{validationContext.DisplayName} must be one of: {validTypes}";
            return new ValidationResult(ErrorMessage ?? message);
        }

        return ValidationResult.Success;
    }
}
