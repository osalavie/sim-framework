using System.Text;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;

namespace Sim.Framework.Domain.Security;

/// <summary>
/// JWT <see cref="SecurityKey"/> backed by an SM2 key pair (GB/T 32918 / sm2p256v1).
/// Signing requires the private key; verification only needs the public key.
/// The JWT signature is raw r||s (64 bytes), similar to ES256, using SM3 as the digest
/// (algorithm: <see cref="GmCryptoAlgorithms.Sm2WithSm3"/>).
/// </summary>
public sealed class Sm2SecurityKey : SecurityKey
{
    private static readonly ECDomainParameters Sm2Domain = CreateSm2Domain();

    /// <summary>Public key parameters (always present).</summary>
    public ECPublicKeyParameters PublicKeyParameters { get; }

    /// <summary>Private key parameters; null when only verification is needed.</summary>
    public ECPrivateKeyParameters? PrivateKeyParameters { get; }

    public Sm2SecurityKey(string publicKeyBase64, string? privateKeyHex, string? kid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyBase64);

        PublicKeyParameters = CreatePublicKey(publicKeyBase64);
        if (!string.IsNullOrWhiteSpace(privateKeyHex))
        {
            PrivateKeyParameters = CreatePrivateKey(privateKeyHex);
        }

        KeyId = kid;
        CryptoProviderFactory = new CryptoProviderFactory
        {
            CustomCryptoProvider = new Sm2CryptoProvider()
        };
    }

    public override int KeySize => 256;

    private static ECDomainParameters CreateSm2Domain()
    {
        var ecParams = GMNamedCurves.GetByName(GmCryptoAlgorithms.Sm2CurveName);
        if (ecParams == null)
        {
            throw new InvalidOperationException(
                "SM2 曲线 '" + GmCryptoAlgorithms.Sm2CurveName + "' 不可用。");
        }

        return new ECDomainParameters(ecParams);
    }

    private static ECPublicKeyParameters CreatePublicKey(string publicKeyBase64)
    {
        var encoded = Convert.FromBase64String(publicKeyBase64);
        return new ECPublicKeyParameters(Sm2Domain.Curve.DecodePoint(encoded), Sm2Domain);
    }

    private static ECPrivateKeyParameters CreatePrivateKey(string privateKeyHex)
    {
        return new ECPrivateKeyParameters(new BigInteger(privateKeyHex, 16), Sm2Domain);
    }
}

/// <summary>
/// <see cref="ICryptoProvider"/> that routes SM2 signing/verification to
/// <see cref="Sm2SignatureProvider"/>. The JWT signing/validation pipeline consults
/// <see cref="CryptoProviderFactory.CustomCryptoProvider"/> before built-in algorithms.
/// </summary>
public sealed class Sm2CryptoProvider : ICryptoProvider
{
    public object Create(string algorithm, params object[] args)
    {
        if (args.Length > 1 && args[0] is Sm2SecurityKey sm2Key && args[1] is bool signing)
        {
            return new Sm2SignatureProvider(sm2Key, algorithm, signing);
        }

        throw new NotSupportedException(
            "SM2 signature provider only supports Sm2SecurityKey keys and algorithm '"
            + GmCryptoAlgorithms.Sm2WithSm3 + "'.");
    }

    public bool IsSupportedAlgorithm(string algorithm, params object[] args)
    {
        return string.Equals(algorithm, GmCryptoAlgorithms.Sm2WithSm3, StringComparison.Ordinal)
               && args.Length > 1
               && args[0] is Sm2SecurityKey;
    }

    public void Release(object cryptoInstance)
    {
        // No pooled resources to release.
    }
}

/// <summary>
/// Signs and verifies JWT signing input using SM2-with-SM3 (raw r||s signature).
/// </summary>
public sealed class Sm2SignatureProvider : SignatureProvider
{
    private static readonly byte[] Sm2UserId = Encoding.UTF8.GetBytes(GmCryptoAlgorithms.DefaultSm2UserId);
    private readonly bool _signing;

    public Sm2SignatureProvider(SecurityKey key, string algorithm, bool signing)
        : base(key, algorithm)
    {
        _signing = signing;
    }

    public override byte[] Sign(byte[] input)
    {
        if (!_signing)
        {
            throw new InvalidOperationException("该 SM2 Provider 仅用于验签。");
        }

        if (Key is not Sm2SecurityKey sm2Key || sm2Key.PrivateKeyParameters == null)
        {
            throw new InvalidOperationException("SM2 签名需要配置私钥。");
        }

        var signer = new SM2Signer(PlainDsaEncoding.Instance);
        signer.Init(true, new ParametersWithID(sm2Key.PrivateKeyParameters, Sm2UserId));
        signer.BlockUpdate(input, 0, input.Length);
        return signer.GenerateSignature();
    }

    public override bool Verify(byte[] input, byte[] signature)
    {
        if (Key is not Sm2SecurityKey sm2Key)
        {
            return false;
        }

        var signer = new SM2Signer(PlainDsaEncoding.Instance);
        signer.Init(false, new ParametersWithID(sm2Key.PublicKeyParameters, Sm2UserId));
        signer.BlockUpdate(input, 0, input.Length);
        return signer.VerifySignature(signature);
    }

    protected override void Dispose(bool disposing)
    {
        // No unmanaged resources to release.
    }
}