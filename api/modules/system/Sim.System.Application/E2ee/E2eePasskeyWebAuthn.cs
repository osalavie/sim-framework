using Fido2NetLib;
using Microsoft.Extensions.Configuration;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.E2ee;

/// <summary>RP ID / Origin 只能来自部署配置，不信任请求 Host 或客户端提供的 Origin 列表。</summary>
public sealed class E2eePasskeyWebAuthn(IConfiguration configuration)
{
    public string RpId => configuration["E2eePasskeys:RpId"]?.Trim() ?? "";
    public bool Enabled => configuration.GetValue<bool>("E2eePasskeys:Enabled") && IsValid();

    private bool IsValid()
    {
        if (Uri.CheckHostName(RpId) == UriHostNameType.Unknown || RpId.Contains(':')) return false;
        var origins = configuration.GetSection("E2eePasskeys:Origins").Get<string[]>() ?? [];
        return origins.Length > 0 && origins.All(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) &&
            uri.GetLeftPart(UriPartial.Authority) == origin &&
            (uri.Host == RpId || uri.Host.EndsWith("." + RpId, StringComparison.OrdinalIgnoreCase)));
    }

    public IFido2 Create() => new Fido2(new Fido2Configuration
    {
        RPID = RpId, RPName = BrandDefaults.Name + " E2EE", ChallengeSize = 32, Timeout = 120_000,
        Origins = (configuration.GetSection("E2eePasskeys:Origins").Get<string[]>() ?? []).ToHashSet(),
        AllowCrossOriginRequests = false
    });
}
