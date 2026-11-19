namespace Sim.Framework.Domain.Security;

/// <summary>
/// Describes a single JWT signing key with its kid (Key ID) identifier.
/// </summary>
public sealed class JwtSigningKeyDescriptor
{
    /// <summary>
    /// Key ID — written into JWT header as "kid". Must be unique across all configured keys.
    /// </summary>
    public string Kid { get; init; } = string.Empty;

    /// <summary>
    /// Signing algorithm. Empty means HMAC-SHA256 (legacy behavior);
    /// set to <see cref="GmCryptoAlgorithms.Sm2WithSm3"/> for SM2 signatures.
    /// </summary>
    public string Algorithm { get; init; } = string.Empty;

    /// <summary>
    /// HMAC secret (plaintext or base64). Must be at least 32 bytes when decoded.
    /// Required for HMAC keys, ignored for SM2 keys.
    /// </summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>
    /// SM2 public key as Base64-encoded uncompressed point (0x04 || X || Y, 65 bytes).
    /// Required for SM2 keys, ignored for HMAC keys.
    /// </summary>
    public string PublicKey { get; init; } = string.Empty;

    /// <summary>
    /// SM2 private key as hex (D value). Only needed for the active signing key;
    /// retired SM2 keys kept for validation may leave it empty.
    /// </summary>
    public string PrivateKey { get; init; } = string.Empty;

    /// <summary>
    /// Whether this is the active key used to sign new tokens.
    /// Only one key should be marked active at a time.
    /// </summary>
    public bool IsActive { get; init; }
}