namespace Sim.Framework.Domain.Shared.Exceptions;

/// <summary>
/// Validation exception for invalid input. Replaces Volo.Abp.Validation.AbpValidationException.
/// Collects multiple validation errors in a single exception.
/// </summary>
public class ValidationException : Exception
{
    public List<string> Errors { get; }

    public ValidationException()
    {
        Errors = [];
    }

    public ValidationException(string message) : base(message)
    {
        Errors = [message];
    }

    public ValidationException(List<string> errors) : base(string.Join("; ", errors))
    {
        Errors = errors;
    }

    public ValidationException(string message, Exception innerException) : base(message, innerException)
    {
        Errors = [message];
    }
}
