using System.Globalization;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Application.Dictionary;

internal static class DataDictionaryValueValidator
{
    public static string ValidateAndNormalize(DataDictionaryValueType valueType, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidValue();
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 1024)
        {
            throw InvalidValue();
        }

        return valueType switch
        {
            DataDictionaryValueType.String => trimmed,
            DataDictionaryValueType.Number => NormalizeNumber(trimmed),
            DataDictionaryValueType.Date => NormalizeDate(trimmed),
            _ => throw InvalidValue()
        };
    }

    private static string NormalizeNumber(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            throw InvalidValue();
        }

        return number.ToString("G29", CultureInfo.InvariantCulture);
    }

    private static string NormalizeDate(string value)
    {
        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            throw InvalidValue();
        }

        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static BusinessException InvalidValue() =>
        new(SystemErrorCodes.InvalidDataDictionaryItem, "数据字典项值与字典值类型不匹配。");
}
