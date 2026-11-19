using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Sim.System.Application.Contracts.Validation;

/// <summary>
/// Validates that a Guid is not empty
/// </summary>
public class ValidGuidAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success; // Null is handled by [Required] attribute
        }

        if (value is Guid guidValue)
        {
            if (guidValue == Guid.Empty)
            {
                var message = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
                    ? $"{validationContext.DisplayName} 不能是空 GUID。"
                    : $"{validationContext.DisplayName} cannot be an empty GUID.";
                return new ValidationResult(ErrorMessage ?? message);
            }
        }

        return ValidationResult.Success;
    }
}
