using Volo.Abp.ExceptionHandling;

namespace Sim.Framework.Domain.Shared.Exceptions;

/// <summary>
/// Thrown when the current user is not authorized to perform an operation.
/// Replaces Volo.Abp.Authorization.AbpAuthorizationException.
/// </summary>
public class AuthorizationException : Exception, IHasErrorCode
{
    public string? Code { get; }

    string IHasErrorCode.Code => Code ?? string.Empty;

    public AuthorizationException()
    {
    }

    public AuthorizationException(string message) : base(message)
    {
    }

    public AuthorizationException(string code, string message) : base(message)
    {
        Code = code;
    }

    public AuthorizationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
