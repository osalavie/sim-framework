using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Application.Security;

public sealed record ExternalLoginStateSession(
    string Provider,
    string State,
    string Nonce,
    string CallbackUrl,
    DateTimeOffset ExpiresAt,
    Guid? BindingUserId = null);

public interface IExternalLoginStateService
{
    Task<ExternalLoginStateSession> BeginAsync(string providerId);

    Task<ExternalLoginStateSession> BeginBindingAsync(string providerId, Guid userId);

    Task<ExternalLoginStateSession> ConsumeAsync(string providerId, string? state);
}

public sealed class ExternalLoginStateService(
    IOptions<ExternalLoginOptions> options,
    IDataProtectionProvider dataProtectionProvider,
    IHttpContextAccessor httpContextAccessor) : IExternalLoginStateService
{
    private const string ProtectorName = "Sim.System.ExternalLogin.v2";
    private const string CookiePath = "/api/system/external-login";

    public Task<ExternalLoginStateSession> BeginAsync(string providerId)
    {
        return BeginCoreAsync(providerId, null);
    }

    public Task<ExternalLoginStateSession> BeginBindingAsync(string providerId, Guid userId)
    {
        return BeginCoreAsync(providerId, userId);
    }

    private Task<ExternalLoginStateSession> BeginCoreAsync(string providerId, Guid? bindingUserId)
    {
        var state = $"{providerId.ToLowerInvariant()}.{RandomNumberGenerator.GetHexString(32).ToLowerInvariant()}";
        var nonce = RandomNumberGenerator.GetHexString(32).ToLowerInvariant();
        var session = new ExternalLoginStateSession(
            providerId,
            state,
            nonce,
            BuildCallbackUrl(),
            DateTimeOffset.UtcNow.AddMinutes(options.Value.StateExpirationMinutes),
            bindingUserId);
        Write(session);
        return Task.FromResult(session);
    }

    public Task<ExternalLoginStateSession> ConsumeAsync(string providerId, string? state)
    {
        var context = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("External login requires an HTTP context.");
        var value = context.Request.Cookies[options.Value.StateCookieName];
        DeleteCookie(context);

        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(state))
        {
            throw ExternalLoginErrors.InvalidState();
        }

        ExternalLoginStateSession session;
        try
        {
            session = JsonSerializer.Deserialize<ExternalLoginStateSession>(
                dataProtectionProvider.CreateProtector(ProtectorName).Unprotect(value)) ??
                throw ExternalLoginErrors.InvalidState();
        }
        catch (Exception exception) when (exception is JsonException or CryptographicException)
        {
            throw ExternalLoginErrors.InvalidState();
        }

        if (!string.Equals(session.Provider, providerId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(session.State, state, StringComparison.Ordinal) ||
            session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw ExternalLoginErrors.InvalidState();
        }

        return Task.FromResult(session);
    }

    private string BuildCallbackUrl()
    {
        var callbackUrl = options.Value.CallbackBaseUrl?.Trim();

        if (string.IsNullOrWhiteSpace(callbackUrl) ||
            !Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
                !(uri.Scheme == Uri.UriSchemeHttp && uri.Host == "localhost")) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new BusinessException(
                SystemErrorCodes.ExternalLoginProviderError,
                "ExternalLogin:CallbackBaseUrl must be a fixed absolute HTTPS URL, or HTTP only on localhost, without query or fragment.");
        }

        return callbackUrl;
    }

    private void Write(ExternalLoginStateSession session)
    {
        var context = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("External login requires an HTTP context.");
        var protectedValue = dataProtectionProvider
            .CreateProtector(ProtectorName)
            .Protect(JsonSerializer.Serialize(session));
        context.Response.Cookies.Append(options.Value.StateCookieName, protectedValue, CreateCookieOptions(session.ExpiresAt));
    }

    private void DeleteCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(options.Value.StateCookieName, CreateCookieOptions(DateTimeOffset.MinValue));
    }

    private CookieOptions CreateCookieOptions(DateTimeOffset expiresAt) => new()
    {
        HttpOnly = true,
        Secure = options.Value.StateCookieSecure,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = CookiePath,
        Expires = expiresAt
    };
}