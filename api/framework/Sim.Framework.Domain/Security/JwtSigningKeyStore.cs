using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Sim.Framework.Domain.Security;

/// <summary>
/// Manages JWT signing keys with kid-based lookup for seamless key rotation.
///
/// Two modes:
/// - Multi-key mode: configured via JwtSettings:SigningKeys array.
///   Active key is used for signing; all keys are used for validation.
///   Each key may be HMAC-SHA256 (Secret) or SM2 (Algorithm=SM2WITHSM3, PublicKey/PrivateKey).
/// - Legacy single-key mode: falls back to JwtSettings:IssuerSigningKey (HMAC-SHA256).
///   No kid is written to the token header in this mode.
/// </summary>
public class JwtSigningKeyStore
{
    private readonly Dictionary<string, SecurityKey> _keys = new();
    private readonly JwtSigningKeyDescriptor? _activeDescriptor;
    private readonly SymmetricSecurityKey? _legacyKey;

    public JwtSigningKeyStore(IConfiguration configuration)
    {
        // Deployment uses key-per-file secrets with the canonical JSON shape
        // JwtSettings:SigningKeys:Keys:[0].Secret. Keep direct-array fallback
        // (JwtSettings:SigningKeys:[0].Secret) for tests and older overrides.
        var keysSection = configuration.GetSection("JwtSettings:SigningKeys:Keys");
        var keyDescriptors = keysSection.Get<List<JwtSigningKeyDescriptor>>();
        if (keyDescriptors is not { Count: > 0 })
        {
            keyDescriptors = configuration
                .GetSection("JwtSettings:SigningKeys")
                .Get<List<JwtSigningKeyDescriptor>>() ?? [];
        }

        if (keyDescriptors.Count > 0)
        {
            var validDescriptors = keyDescriptors
                .Where(IsValidDescriptor)
                .ToList();

            if (validDescriptors.Count > 0)
            {
                foreach (var kd in validDescriptors)
                {
                    _keys[kd.Kid] = CreateSecurityKey(kd);

                    if (kd.IsActive)
                    {
                        if (_activeDescriptor != null)
                        {
                            throw new InvalidOperationException(
                                $"Multiple active JWT signing keys detected: '{_activeDescriptor.Kid}' and '{kd.Kid}'. " +
                                "Only one key may be marked IsActive=true.");
                        }

                        _activeDescriptor = kd;
                    }
                }

                if (_activeDescriptor == null)
                {
                    throw new InvalidOperationException(
                        "JwtSettings:SigningKeys is configured but no key is marked IsActive=true.");
                }
            }
            // else: section exists but has no valid keys (e.g. template with empty secrets).
            // Gracefully fall through to legacy mode.
        }

        // Legacy fallback: single IssuerSigningKey (always available for backward compatibility)
        var legacySecret = configuration["JwtSettings:IssuerSigningKey"];
        if (!string.IsNullOrWhiteSpace(legacySecret) && Encoding.UTF8.GetByteCount(legacySecret) >= 32)
        {
            _legacyKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(legacySecret));
        }
    }

    /// <summary>
    /// Whether multi-key mode is active (kid will be written to token headers).
    /// </summary>
    public bool IsMultiKeyMode => _activeDescriptor != null;

    /// <summary>
    /// The active signing key. In multi-key mode this is the key marked IsActive=true;
    /// in legacy mode this is the single IssuerSigningKey.
    /// </summary>
    public SecurityKey? GetActiveKey() =>
        (_activeDescriptor != null && _keys.TryGetValue(_activeDescriptor.Kid, out var activeKey))
            ? activeKey
            : _legacyKey;

    /// <summary>
    /// The algorithm used to sign new tokens. SM2 keys require Algorithm=SM2WITHSM3;
    /// HMAC and legacy mode use HMAC-SHA256.
    /// </summary>
    public string GetActiveAlgorithm()
    {
        if (_activeDescriptor != null && !string.IsNullOrWhiteSpace(_activeDescriptor.Algorithm))
        {
            return _activeDescriptor.Algorithm;
        }

        return SecurityAlgorithms.HmacSha256;
    }

    /// <summary>
    /// The kid of the active key, or null in legacy single-key mode.
    /// </summary>
    public string? GetActiveKid() => _activeDescriptor?.Kid;

    /// <summary>
    /// Resolves signing keys for token validation. Looks up by kid first,
    /// then falls back to all available keys (legacy mode or unknown kid).
    /// </summary>
    public IEnumerable<SecurityKey> ResolveSigningKeys(string? kid)
    {
        if (!string.IsNullOrEmpty(kid) && _keys.TryGetValue(kid, out var matchedKey))
        {
            yield return matchedKey;
        }

        // Also include the legacy key for backward compatibility
        if (_legacyKey != null)
        {
            yield return _legacyKey;
        }

        // If no kid match and no legacy key, return all known keys
        if ((string.IsNullOrEmpty(kid) || !_keys.ContainsKey(kid)) && _legacyKey == null)
        {
            foreach (var key in _keys.Values)
            {
                yield return key;
            }
        }
    }

    private static bool IsValidDescriptor(JwtSigningKeyDescriptor kd)
    {
        if (string.IsNullOrWhiteSpace(kd.Kid))
        {
            return false;
        }

        if (string.Equals(kd.Algorithm, GmCryptoAlgorithms.Sm2WithSm3, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(kd.PublicKey);
        }

        return !string.IsNullOrWhiteSpace(kd.Secret)
               && Encoding.UTF8.GetByteCount(kd.Secret) >= 32;
    }

    private static SecurityKey CreateSecurityKey(JwtSigningKeyDescriptor kd)
    {
        if (string.Equals(kd.Algorithm, GmCryptoAlgorithms.Sm2WithSm3, StringComparison.OrdinalIgnoreCase))
        {
            return new Sm2SecurityKey(kd.PublicKey, kd.PrivateKey, kd.Kid);
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(kd.Secret));
    }
}