using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.User;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Application.Security;

public sealed record ExternalLoginRedirectContext(
    string CallbackUrl,
    string State,
    string Nonce);

public sealed record ExternalLoginAuthenticationContext(
    ExternalLoginCallbackRequest Request,
    string CallbackUrl,
    string Nonce,
    IReadOnlyDictionary<string, string> Query);

public sealed record ExternalLoginIdentity(
    string ProviderKey,
    string? UserName,
    string? DisplayName,
    string? Email = null);

public interface IExternalLoginProvider
{
    string Id { get; }

    string DisplayName { get; }

    bool Enabled { get; }

    Task<string> BuildRedirectUrlAsync(
        ExternalLoginRedirectContext context,
        CancellationToken cancellationToken = default);

    Task<ExternalLoginIdentity> AuthenticateAsync(
        ExternalLoginAuthenticationContext context,
        CancellationToken cancellationToken = default);
}

public interface IExternalLoginProviderRegistry
{
    IReadOnlyList<IExternalLoginProvider> EnabledProviders { get; }

    IExternalLoginProvider Get(string providerId);
}

public sealed class ExternalLoginProviderRegistry(IEnumerable<IExternalLoginProvider> providers) : IExternalLoginProviderRegistry
{
    private readonly Dictionary<string, IExternalLoginProvider> providers =
        providers.ToDictionary(provider => provider.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IExternalLoginProvider> EnabledProviders =>
        providers.Values.Where(provider => provider.Enabled).OrderBy(provider => provider.Id).ToList();

    public IExternalLoginProvider Get(string providerId)
    {
        var id = providerId.Trim();
        return providers.TryGetValue(id, out var provider) && provider.Enabled
            ? provider
            : throw ExternalLoginErrors.ProviderDisabled(id);
    }
}

public static class ExternalLoginErrors
{
    public static BusinessException ProviderDisabled(string provider) => new(
        Domain.Shared.ErrorCodes.SystemErrorCodes.ExternalLoginProviderDisabled,
        $"The external login provider '{provider}' is disabled.");

    public static BusinessException ProviderUnavailable() => new(
        Domain.Shared.ErrorCodes.SystemErrorCodes.ExternalLoginProviderError,
        "The external login provider is unavailable.");

    public static BusinessException InvalidState() => new(
        Domain.Shared.ErrorCodes.SystemErrorCodes.InvalidExternalLoginState,
        "The external login state is invalid or expired.");

    public static BusinessException InvalidIdentity() => new(
        Domain.Shared.ErrorCodes.SystemErrorCodes.InvalidExternalLoginIdentity,
        "The external login identity is invalid.");
}

public static class ExternalLoginSecurity
{
    public static string CreateProviderKeyHash(string provider, string providerKey)
    {
        var bytes = global::System.Security.Cryptography.SHA256.HashData(
            global::System.Text.Encoding.UTF8.GetBytes(provider + ":" + providerKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
