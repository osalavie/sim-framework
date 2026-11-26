using System.Globalization;
using System.Text;

namespace Sim.Framework.Domain.Shared.Numbering;

/// <summary>
/// A database-managed business-number definition.
/// </summary>
public sealed record BusinessNumberDefinition(
    string BusinessKey,
    string Format,
    long InitialSequence,
    bool Enabled);

/// <summary>
/// Stable port for resolving the database-managed definition of a business-number scenario.
/// </summary>
public interface IBusinessNumberDefinitionStore
{
    Task<BusinessNumberDefinition?> FindAsync(
        string businessKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Requests one number for a named business system. Its format is always resolved from the managed database record.
/// </summary>
public sealed record NumberGenerationRequest(
    string BusinessKey,
    DateTimeOffset? OccurredAt = null);

/// <summary>
/// Generates unique, formatted business numbers. Implementations must make sequence allocation safe across instances.
/// </summary>
public interface IBusinessNumberGenerator
{
    Task<string> GenerateAsync(
        NumberGenerationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Renders and validates business-number templates.
/// </summary>
public static class NumberFormatRenderer
{
    private const string DefaultDateFormat = "yyyyMMdd";

    /// <summary>
    /// Renders a template using standard .NET date and numeric format strings.
    /// </summary>
    public static string Render(
        string format,
        string businessKey,
        DateTimeOffset occurredAt,
        long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        var tokens = Parse(format);
        var builder = new StringBuilder(format.Length + 16);

        foreach (var token in tokens)
        {
            if (token.Kind == TokenKind.Literal)
            {
                builder.Append(token.Value);
                continue;
            }

            builder.Append(token.Kind switch
            {
                TokenKind.Business => businessKey,
                TokenKind.Date => occurredAt.ToString(token.Value ?? DefaultDateFormat, CultureInfo.InvariantCulture),
                TokenKind.Sequence => sequence.ToString(token.Value, CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException("Unknown business-number template token.")
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// Creates the reset scope for a template. Date placeholders form the scope, so a yyyy/MM/dd template resets daily,
    /// yyyy/MM resets monthly, and yyyy resets yearly. A template without a date placeholder has one continuous sequence.
    /// </summary>
    public static string GetSequenceScope(string format, DateTimeOffset occurredAt)
    {
        var dateValues = Parse(format)
            .Where(token => token.Kind == TokenKind.Date)
            .Select(token => occurredAt.ToString(token.Value ?? DefaultDateFormat, CultureInfo.InvariantCulture))
            .ToArray();

        return dateValues.Length == 0 ? "global" : string.Join('\u001F', dateValues);
    }

    private static IReadOnlyList<TemplateToken> Parse(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new FormatException("The business-number format cannot be empty.");
        }

        var tokens = new List<TemplateToken>();
        var cursor = 0;
        var sequenceCount = 0;
        while (cursor < format.Length)
        {
            var openingBrace = format.IndexOf('{', cursor);
            if (openingBrace < 0)
            {
                AddLiteral(format[cursor..], tokens);
                break;
            }

            AddLiteral(format[cursor..openingBrace], tokens);
            var closingBrace = format.IndexOf('}', openingBrace + 1);
            if (closingBrace < 0)
            {
                throw new FormatException("The business-number format contains an unclosed placeholder.");
            }

            var placeholder = format[(openingBrace + 1)..closingBrace];
            if (placeholder.Contains('{') || placeholder.Contains('}') || string.IsNullOrWhiteSpace(placeholder))
            {
                throw new FormatException("The business-number format contains an invalid placeholder.");
            }

            var separator = placeholder.IndexOf(':');
            var name = (separator < 0 ? placeholder : placeholder[..separator]).Trim();
            var value = separator < 0 ? null : placeholder[(separator + 1)..];
            if (value is { Length: 0 })
            {
                throw new FormatException($"The '{{{name}}}' placeholder format cannot be empty.");
            }

            switch (name.ToLowerInvariant())
            {
                case "business" when value is null:
                    tokens.Add(new TemplateToken(TokenKind.Business, null));
                    break;
                case "date":
                    tokens.Add(new TemplateToken(TokenKind.Date, value));
                    break;
                case "sequence":
                    sequenceCount++;
                    tokens.Add(new TemplateToken(TokenKind.Sequence, value));
                    break;
                default:
                    throw new FormatException(
                        $"Unsupported business-number placeholder '{{{placeholder}}}'. Use business, date or sequence.");
            }

            cursor = closingBrace + 1;
        }

        if (sequenceCount != 1)
        {
            throw new FormatException("The business-number format must contain exactly one {sequence} placeholder.");
        }

        return tokens;
    }

    private static void AddLiteral(string value, ICollection<TemplateToken> tokens)
    {
        if (value.Contains('}'))
        {
            throw new FormatException("The business-number format contains an unmatched closing brace.");
        }

        if (!string.IsNullOrEmpty(value))
        {
            tokens.Add(new TemplateToken(TokenKind.Literal, value));
        }
    }

    private enum TokenKind
    {
        Literal,
        Business,
        Date,
        Sequence
    }

    private sealed record TemplateToken(TokenKind Kind, string? Value);
}
