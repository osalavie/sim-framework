using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Sim.Framework.Domain.Security;

namespace Sim.Security.Tests;

public class GmCryptoTests
{
    private readonly IGmCryptoService _gm = new GmCryptoService();

    [Fact]
    public void Sm2KeyPairHasExpectedFormats()
    {
        var (publicKey, privateKey) = _gm.GenerateSm2KeyPair();

        var publicBytes = Convert.FromBase64String(publicKey);
        Assert.Equal(65, publicBytes.Length);
        Assert.Equal(0x04, publicBytes[0]);
        Assert.False(string.IsNullOrWhiteSpace(privateKey));
    }

    [Fact]
    public void Sm2SignVerifyRoundTrip()
    {
        var (publicKey, privateKey) = _gm.GenerateSm2KeyPair();
        const string data = "国密签名测试 payload-123";

        var signature = _gm.Sm2Sign(privateKey, data);

        Assert.True(_gm.Sm2Verify(publicKey, data, signature));
        Assert.False(_gm.Sm2Verify(publicKey, data + "tampered", signature));
    }

    [Fact]
    public void Sm2EncryptDecryptRoundTrip()
    {
        var (publicKey, privateKey) = _gm.GenerateSm2KeyPair();
        const string plainText = "国密加密测试 Hello SM2 中文";

        var cipherText = _gm.Sm2Encrypt(publicKey, plainText);

        Assert.NotEqual(plainText, cipherText);
        Assert.Equal(plainText, _gm.Sm2Decrypt(privateKey, cipherText));
    }

    [Fact]
    public void Sm4EngineMatchesStandardKnownVector()
    {
        // GB/T 32907 标准 SM4 已知向量（ECB 单分组、无填充）
        var key = Convert.FromHexString("0123456789abcdeffedcba9876543210");
        var plain = Convert.FromHexString("0123456789abcdeffedcba9876543210");

        var cipher = new SM4Engine();
        cipher.Init(true, new KeyParameter(key));
        var output = new byte[16];
        cipher.ProcessBlock(plain, 0, output, 0);

        Assert.Equal("681EDF34D206965E86B3E94F536E4246", Convert.ToHexString(output));
    }

    [Fact]
    public void Sm4EcbRoundTripsTextPayload()
    {
        var key = _gm.GenerateSm4Key();
        const string plainText = "SM4 ECB 字符串加解密 0123456789";

        var cipherText = _gm.Sm4Encrypt(key, plainText);

        Assert.NotEqual(plainText, cipherText);
        Assert.Equal(plainText, _gm.Sm4Decrypt(key, cipherText));
    }

    [Fact]
    public void Sm4CbcWithIvRoundTripsAndDiffersFromEcb()
    {
        var key = _gm.GenerateSm4Key();
        var iv = _gm.GenerateSm4Key();
        const string plainText = "SM4 CBC 分组加密测试 123";

        var ecb = _gm.Sm4Encrypt(key, plainText);
        var cbc = _gm.Sm4Encrypt(key, plainText, iv);

        Assert.NotEqual(ecb, cbc);
        Assert.Equal(plainText, _gm.Sm4Decrypt(key, ecb));
        Assert.Equal(plainText, _gm.Sm4Decrypt(key, cbc, iv));
    }

    [Fact]
    public void Sm3MatchesKnownVector()
    {
        // GB/T 32905 标准测试向量：SM3("abc")
        var expected = Convert.ToBase64String(Convert.FromHexString(
            "66c7f0f462eeedd9d1f2d46bdc10e4e24167c4875cf2f7a2297da02b8f4ba8e0"));

        Assert.Equal(expected, _gm.GenerateSm3Hash("abc"));
    }
}