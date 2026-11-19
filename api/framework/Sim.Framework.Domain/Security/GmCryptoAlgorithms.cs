namespace Sim.Framework.Domain.Security;

/// <summary>
/// Constants for GM (Chinese national cryptography, 国密) algorithms used across the framework.
/// </summary>
public static class GmCryptoAlgorithms
{
    /// <summary>
    /// JWT <c>alg</c> value for SM2 signature with SM3 digest (GB/T 32918.2).
    /// Set <c>JwtSettings:SigningKeys[*]:Algorithm</c> to this value to enable SM2-signed tokens.
    /// </summary>
    public const string Sm2WithSm3 = "SM2WITHSM3";

    /// <summary>
    /// Default SM2 user ID defined by GB/T 32918.2 ("1234567812345678").
    /// Must be identical on both signing and verification.
    /// </summary>
    public const string DefaultSm2UserId = "1234567812345678";

    /// <summary>
    /// Name of the SM2 elliptic curve (sm2p256v1).
    /// </summary>
    public const string Sm2CurveName = "sm2p256v1";
}