using System.Globalization;

namespace Sim.DeviceAgent.Codecs;

public static class ValueCoercion
{
    public static object? Coerce(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        if (value.Length > 1 && value[0] == '0' && char.IsDigit(value[1]))
        {
            return value;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return value;
    }
}
