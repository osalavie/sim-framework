using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sim.System.Domain.Shared;

namespace Sim.System.Application.Security;

public static class ExternalLoginServiceCollectionExtensions
{
    public static IServiceCollection AddExternalLogin(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient("ExternalLoginProvider")
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });

        services
            .AddOptions<ExternalLoginOptions>()
            .Bind(configuration.GetSection("ExternalLogin"))
            .Validate(options => options.StateExpirationMinutes > 0,
                "ExternalLogin:StateExpirationMinutes must be greater than zero.")
            .Validate(options => IsOAuth2Valid(options.WeChat),
                "ExternalLogin:WeChat requires ClientId and ClientSecret when enabled.")
            .Validate(options => IsOAuth2Valid(options.QQ),
                "ExternalLogin:QQ requires ClientId and ClientSecret when enabled.")
            .Validate(options => IsOidcValid(options.Microsoft),
                "ExternalLogin:Microsoft requires a HTTPS Authority, ClientId, ClientSecret and the openid scope when enabled.")
            .Validate(options => IsOidcValid(options.Google),
                "ExternalLogin:Google requires a HTTPS Authority, ClientId, ClientSecret and the openid scope when enabled.")
            .ValidateOnStart();

        services.AddSingleton<IOidcConfigurationManagerFactory, OidcConfigurationManagerFactory>();
        services.AddScoped<IExternalLoginProviderRegistry, ExternalLoginProviderRegistry>();
        services.AddScoped<IExternalLoginStateService, ExternalLoginStateService>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IExternalLoginProvider, WeChatExternalLoginProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IExternalLoginProvider, QQExternalLoginProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IExternalLoginProvider, SteamExternalLoginProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IExternalLoginProvider, MicrosoftExternalLoginProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IExternalLoginProvider, GoogleExternalLoginProvider>());
        return services;
    }

    private static bool IsOAuth2Valid(OAuth2ExternalLoginProviderOptions provider) =>
        !provider.Enabled ||
        (!string.IsNullOrWhiteSpace(provider.ClientId) &&
         !string.IsNullOrWhiteSpace(provider.ClientSecret));

    private static bool IsOidcValid(OidcExternalLoginProviderOptions provider) =>
        !provider.Enabled ||
        (!string.IsNullOrWhiteSpace(provider.Authority) &&
         Uri.TryCreate(provider.Authority, UriKind.Absolute, out var authority) &&
         authority.Scheme == Uri.UriSchemeHttps &&
         !string.IsNullOrWhiteSpace(provider.ClientId) &&
         !string.IsNullOrWhiteSpace(provider.ClientSecret) &&
         provider.Scopes.Contains("openid", StringComparer.Ordinal));
}
