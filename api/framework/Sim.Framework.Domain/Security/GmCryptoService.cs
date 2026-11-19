using System.Text;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Sim.Framework.Domain.Services;

namespace Sim.Framework.Domain.Security;

/// <summary>
/// GM (国密) cryptographic operations built on BouncyCastle:
/// SM2 签名/验签与加解密、SM4 分组加解密（ECB/CBC + PKCS7）、SM3 哈希。
/// 字符串级 API：密钥与密文使用 Base64，SM2 私钥使用 hex。
/// SM2 签名格式为原始 r||s（64 字节），SM2 密文采用 GB/T 32918.4 推荐的 C1C3C2。
/// </summary>
public interface IGmCryptoService : IDomainService
{
    (string PublicKey, string PrivateKey) GenerateSm2KeyPair();
    string GenerateSm4Key();
    string GenerateSm3Hash(string data);
    string Sm2Sign(string privateKeyHex, string data);
    bool Sm2Verify(string publicKeyBase64, string data, string signatureBase64);
    string Sm2Encrypt(string publicKeyBase64, string plainText);
    string Sm2Decrypt(string privateKeyHex, string cipherTextBase64);
    string Sm4Encrypt(string keyBase64, string plainText, string? ivBase64 = null);
    string Sm4Decrypt(string keyBase64, string cipherTextBase64, string? ivBase64 = null);
}

public class GmCryptoService : DomainService, IGmCryptoService
{
    private static readonly ECDomainParameters Sm2Domain = CreateSm2Domain();
    private static readonly byte[] Sm2UserId = Encoding.UTF8.GetBytes(GmCryptoAlgorithms.DefaultSm2UserId);

    public (string PublicKey, string PrivateKey) GenerateSm2KeyPair()
    {
        var keyGen = new ECKeyPairGenerator();
        keyGen.Init(new ECKeyGenerationParameters(Sm2Domain, new SecureRandom()));
        var keyPair = keyGen.GenerateKeyPair();

        // 未压缩公钥（0x04 || X || Y，65 字节），私钥为 D 的 hex
        var publicKey = ((ECPublicKeyParameters)keyPair.Public).Q.GetEncoded(false);
        var privateKey = ((ECPrivateKeyParameters)keyPair.Private).D.ToString(16);

        return (Convert.ToBase64String(publicKey), privateKey);
    }

    public string GenerateSm4Key()
    {
        var key = new byte[16];
        new SecureRandom().NextBytes(key);
        return Convert.ToBase64String(key);
    }

    public string GenerateSm3Hash(string data)
    {
        var digest = new SM3Digest();
        var bytes = Encoding.UTF8.GetBytes(data);
        digest.BlockUpdate(bytes, 0, bytes.Length);
        var hash = new byte[digest.GetDigestSize()];
        digest.DoFinal(hash, 0);
        return Convert.ToBase64String(hash);
    }

    public string Sm2Sign(string privateKeyHex, string data)
    {
        var signer = new SM2Signer(PlainDsaEncoding.Instance);
        signer.Init(true, new ParametersWithID(CreatePrivateKey(privateKeyHex), Sm2UserId));
        var bytes = Encoding.UTF8.GetBytes(data);
        signer.BlockUpdate(bytes, 0, bytes.Length);
        return Convert.ToBase64String(signer.GenerateSignature());
    }

    public bool Sm2Verify(string publicKeyBase64, string data, string signatureBase64)
    {
        var signer = new SM2Signer(PlainDsaEncoding.Instance);
        signer.Init(false, new ParametersWithID(CreatePublicKey(publicKeyBase64), Sm2UserId));
        var bytes = Encoding.UTF8.GetBytes(data);
        signer.BlockUpdate(bytes, 0, bytes.Length);
        return signer.VerifySignature(Convert.FromBase64String(signatureBase64));
    }

    public string Sm2Encrypt(string publicKeyBase64, string plainText)
    {
        var engine = new SM2Engine(SM2Engine.Mode.C1C3C2);
        engine.Init(true, CreatePublicKey(publicKeyBase64));
        var input = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(engine.ProcessBlock(input, 0, input.Length));
    }

    public string Sm2Decrypt(string privateKeyHex, string cipherTextBase64)
    {
        var engine = new SM2Engine(SM2Engine.Mode.C1C3C2);
        engine.Init(false, CreatePrivateKey(privateKeyHex));
        var input = Convert.FromBase64String(cipherTextBase64);
        return Encoding.UTF8.GetString(engine.ProcessBlock(input, 0, input.Length));
    }

    public string Sm4Encrypt(string keyBase64, string plainText, string? ivBase64 = null)
    {
        var cipher = CreateSm4Cipher(ivBase64 != null);
        cipher.Init(true, CreateSm4Parameters(keyBase64, ivBase64));
        var input = Encoding.UTF8.GetBytes(plainText);
        var output = new byte[cipher.GetOutputSize(input.Length)];
        var len = cipher.ProcessBytes(input, 0, input.Length, output, 0);
        len += cipher.DoFinal(output, len);
        return Convert.ToBase64String(output, 0, len);
    }

    public string Sm4Decrypt(string keyBase64, string cipherTextBase64, string? ivBase64 = null)
    {
        var cipher = CreateSm4Cipher(ivBase64 != null);
        cipher.Init(false, CreateSm4Parameters(keyBase64, ivBase64));
        var input = Convert.FromBase64String(cipherTextBase64);
        var output = new byte[cipher.GetOutputSize(input.Length)];
        var len = cipher.ProcessBytes(input, 0, input.Length, output, 0);
        len += cipher.DoFinal(output, len);
        return Encoding.UTF8.GetString(output, 0, len);
    }

    private static PaddedBufferedBlockCipher CreateSm4Cipher(bool useCbc)
    {
        return useCbc
            ? new PaddedBufferedBlockCipher(new CbcBlockCipher(new SM4Engine()), new Pkcs7Padding())
            : new PaddedBufferedBlockCipher(new SM4Engine(), new Pkcs7Padding());
    }

    private static ICipherParameters CreateSm4Parameters(string keyBase64, string? ivBase64)
    {
        var key = new KeyParameter(Convert.FromBase64String(keyBase64));
        if (ivBase64 == null)
        {
            return key;
        }

        var iv = Convert.FromBase64String(ivBase64);
        if (iv.Length != 16)
        {
            throw new ArgumentException("SM4 CBC IV 必须是 16 字节。", nameof(ivBase64));
        }

        return new ParametersWithIV(key, iv);
    }

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
        var point = Sm2Domain.Curve.DecodePoint(encoded);
        return new ECPublicKeyParameters(point, Sm2Domain);
    }

    private static ECPrivateKeyParameters CreatePrivateKey(string privateKeyHex)
    {
        return new ECPrivateKeyParameters(new BigInteger(privateKeyHex, 16), Sm2Domain);
    }
}