using Volo.Abp.ExceptionHandling;

namespace Sim.Framework.Domain.Shared.Exceptions;

/// <summary>
/// General business logic exception. Replaces Volo.Abp.BusinessException.
/// Carries an optional error code and serializable data for API responses.
/// </summary>
public class BusinessException : Exception, IHasErrorCode
{
    public string? Code { get; }

    string IHasErrorCode.Code => Code ?? string.Empty;

    public BusinessException()
    {
    }

    public BusinessException(string message) : base(message)
    {
    }

    public BusinessException(string code, string message) : base(message)
    {
        Code = code;
    }

    public BusinessException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public BusinessException(string code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>
    /// Attach a serializable data item to the exception (fluent). Replaces Volo.Abp.BusinessException.WithData.
    /// </summary>
    public BusinessException WithData(string key, object? value)
    {
        Data[key] = value;
        return this;
    }
}
