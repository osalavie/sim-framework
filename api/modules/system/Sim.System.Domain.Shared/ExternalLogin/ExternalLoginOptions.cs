namespace Sim.System.Domain.Shared;

public sealed class ExternalLoginOptions
{
    public string CallbackBaseUrl { get; set; } = string.Empty;

    public string StateCookieName { get; set; } = "sim_external_login_state";

    public bool StateCookieSecure { get; set; } = true;

    public int StateExpirationMinutes { get; set; } = 10;

    public bool CreateUnlinkedAccounts { get; set; } = true;

    public WeChatExternalLoginProviderOptions WeChat { get; set; } = new();

    public QQExternalLoginProviderOptions QQ { get; set; } = new();

    public SteamExternalLoginProviderOptions Steam { get; set; } = new();

    public OidcExternalLoginProviderOptions Microsoft { get; set; } = new()
    {
        Authority = "https://login.microsoftonline.com/common/v2.0"
    };

    public OidcExternalLoginProviderOptions Google { get; set; } = new()
    {
        Authority = "https://accounts.google.com"
    };
}

public abstract class ExternalLoginProviderOptions
{
    public bool Enabled { get; set; }
}

public abstract class OAuth2ExternalLoginProviderOptions : ExternalLoginProviderOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}

public sealed class WeChatExternalLoginProviderOptions : OAuth2ExternalLoginProviderOptions;

public sealed class QQExternalLoginProviderOptions : OAuth2ExternalLoginProviderOptions;

public sealed class SteamExternalLoginProviderOptions : ExternalLoginProviderOptions
{
    public string? Realm { get; set; }
}

public sealed class OidcExternalLoginProviderOptions : ExternalLoginProviderOptions
{
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string[] Scopes { get; set; } = ["openid", "profile", "email"];
}
