using System.Text;

namespace Sim.DeviceAgent.Codecs;

public static class CsvLineParser
{
    public static List<string> ParseLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];
            if (inQuotes)
            {
                if (current == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        value.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    value.Append(current);
                }
            }
            else if (current == '"')
            {
                inQuotes = true;
            }
            else if (current == delimiter)
            {
                fields.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(current);
            }
        }

        fields.Add(value.ToString());
        return fields;
    }
}
