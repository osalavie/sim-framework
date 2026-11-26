using Volo.Abp.ExceptionHandling;

namespace Sim.Framework.Domain.Shared.Exceptions;

/// <summary>
/// Thrown when the current user is authenticated but lacks the permission required
/// for an operation. Mapped to HTTP 403 by the API exception filter.
/// </summary>
public class ForbiddenException : Exception, IHasErrorCode
{
    public string? Code { get; }

    string IHasErrorCode.Code => Code ?? string.Empty;

    public ForbiddenException()
    {
    }

    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string code, string message) : base(message)
    {
        Code = code;
    }
}
