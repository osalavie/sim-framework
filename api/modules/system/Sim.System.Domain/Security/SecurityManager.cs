using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Sim.Framework.Domain.Security;
using Sim.System.Domain.Shared;
using Sim.Framework.Domain.Services;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Security;
using Sim.System.Domain.User;

namespace Sim.System.Domain.Security;

public interface ISecurityManager : IDomainService
{
    Task<(bool IsValid, List<string> Errors, int MinLength)> ValidatePasswordAsync(string password);
    (string EncryptedPassword, string Salt) EncryptPassword(string password);
    bool VerifyPassword(UserEntity user, string password);
    (string PublicKey, string PrivateKey) GenerateSm2KeyPair();
    string GenerateAccessToken(IEnumerable<Claim> claims);
    string GenerateRefreshToken();
    string GenerateSm4Key();
}

public class SecurityManager : DomainService, ISecurityManager
{
    private readonly IPasswordPolicyProvider _passwordPolicyProvider;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<UserEntity> _passwordHasher;
    private readonly JwtSigningKeyStore _keyStore;

    public SecurityManager(
        IPasswordPolicyProvider passwordPolicyProvider,
        IConfiguration configuration,
        IPasswordHasher<UserEntity> passwordHasher,
        JwtSigningKeyStore keyStore)
    {
        _passwordPolicyProvider = passwordPolicyProvider;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
        _keyStore = keyStore;
    }

    public async Task<(bool IsValid, List<string> Errors, int MinLength)> ValidatePasswordAsync(
        string password)
    {
        var policy = await _passwordPolicyProvider.GetAsync();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add(SystemErrorCodes.PasswordRequired);
            return (false, errors, policy.MinLength);
        }

        if (password.Length < policy.MinLength)
        {
            errors.Add(SystemErrorCodes.PasswordTooShort);
        }

        if (policy.RequireUppercase && !Regex.IsMatch(password, @"[A-Z]"))
        {
            errors.Add(SystemErrorCodes.PasswordRequiresUppercase);
        }

        if (policy.RequireLowercase && !Regex.IsMatch(password, @"[a-z]"))
        {
            errors.Add(SystemErrorCodes.PasswordRequiresLowercase);
        }

        if (policy.RequireDigit && !Regex.IsMatch(password, @"[0-9]"))
        {
            errors.Add(SystemErrorCodes.PasswordRequiresDigit);
        }

        if (policy.RequireSpecialChar && !Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
        {
            errors.Add(SystemErrorCodes.PasswordRequiresSpecialCharacter);
        }

        return (errors.Count == 0, errors, policy.MinLength);
    }

    public (string EncryptedPassword, string Salt) EncryptPassword(string password)
    {
        // 验证密码强度
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ArgumentException("密码长度不能少于6位");
        }

        var user = new UserEntity();
        return (_passwordHasher.HashPassword(user, password), string.Empty);
    }

    public bool VerifyPassword(UserEntity user, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(user.EncryptedPassword))
        {
            return false;
        }

        try
        {
            // 新密码使用 ASP.NET Core PasswordHasher（PBKDF2，含随机盐与版本信息）。
            if (string.IsNullOrWhiteSpace(user.Salt))
            {
                return _passwordHasher.VerifyHashedPassword(user, user.EncryptedPassword, password)
                    != PasswordVerificationResult.Failed;
            }

            // 兼容旧版 SM3 哈希；成功登录后由应用服务自动升级为新格式。
            // 使用存储的盐进行验证，同样的迭代次数
            SM3Digest digest = new SM3Digest();
            byte[] saltedPwdBytes = Encoding.UTF8.GetBytes(password + user.Salt);

            // 多次哈希验证
            for (int i = 0; i < 10000; i++)
            {
                digest.Reset();
                digest.BlockUpdate(saltedPwdBytes, 0, saltedPwdBytes.Length);
                byte[] tempHash = new byte[digest.GetDigestSize()];
                digest.DoFinal(tempHash, 0);
                saltedPwdBytes = tempHash;
            }

            var expectedHash = Convert.FromBase64String(user.EncryptedPassword);
            return expectedHash.Length == saltedPwdBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(expectedHash, saltedPwdBytes);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public (string PublicKey, string PrivateKey) GenerateSm2KeyPair()
    {
        var ecParams = GMNamedCurves.GetByName("sm2p256v1"); // 使用标准曲线
        var keyGen = new ECKeyPairGenerator();
        keyGen.Init(new ECKeyGenerationParameters(new ECDomainParameters(ecParams), new SecureRandom()));

        var keyPair = keyGen.GenerateKeyPair();
        var publicKey = ((ECPublicKeyParameters)keyPair.Public).Q.GetEncoded(false);    // 未压缩公钥
        var privateKey = ((ECPrivateKeyParameters)keyPair.Private).D.ToString(16);    // 私钥Hex

        return (Convert.ToBase64String(publicKey), privateKey);
    }

    public string GenerateSm4Key()
    {
        // SM4 密钥长度为 16 字节
        var secureRandom = new SecureRandom();
        var key = new byte[16];
        secureRandom.NextBytes(key);
        return Convert.ToBase64String(key);
    }

    public string GenerateSm3Hash(string dataToHash)
    {
        var data = Encoding.UTF8.GetBytes(dataToHash);
        var digest = new SM3Digest();
        digest.BlockUpdate(data, 0, data.Length);
        byte[] hash = new byte[digest.GetDigestSize()];
        digest.DoFinal(hash, 0);
        return Convert.ToBase64String(hash);
    }

    public string GenerateAccessToken(IEnumerable<Claim> claims)
    {
        var activeKey = _keyStore.GetActiveKey();
        if (activeKey == null)
        {
            throw new InvalidOperationException(
                "No JWT signing key is configured. " +
                "Configure either JwtSettings:IssuerSigningKey (single key) or " +
                "JwtSettings:SigningKeys (multi-key with kid).");
        }

        // 算法由密钥配置决定：HMAC-SHA256（默认）或 SM2WITHSM3（国密 SM2 签名）
        var signingCredentials = new SigningCredentials(activeKey, _keyStore.GetActiveAlgorithm());

        // Write kid to header when using multi-key mode
        var kid = _keyStore.GetActiveKid();
        var header = new JwtHeader(signingCredentials);
        if (kid != null)
        {
            header["kid"] = kid;
        }

        var claimsList = claims.ToList();
        var payload = new JwtPayload(
            _configuration["JwtSettings:Issuer"],
            _configuration["JwtSettings:Audience"],
            claimsList,
            notBefore: null,
            expires: DateTime.UtcNow.AddMinutes(
                _configuration.GetValue<int>("JwtSettings:AccessTokenExpirationMinutes")));

        var token = new JwtSecurityToken(header, payload);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
