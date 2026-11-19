using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.E2ee;

public sealed class E2eePasskeyConfigurationDto
{
    public bool Enabled { get; set; }
    public string RpId { get; set; } = "";
}

public sealed class E2eePasskeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string RpId { get; set; } = "";
    public int KeyVersion { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public sealed class E2eePasskeyOptionsDto
{
    public Guid ChallengeId { get; set; }
    public int KeyVersion { get; set; }
    public string PrfSalt { get; set; } = "";
    public JsonElement PublicKey { get; set; }
}

public sealed class CompleteE2eePasskeyRegistrationDto
{
    public Guid ChallengeId { get; set; }
    [Required, StringLength(100, MinimumLength = 1)] public string Name { get; set; } = "";
    /// <summary>仅 attestation 公开材料，不得包含 PRF 输出。</summary>
    public JsonElement Credential { get; set; }
    [Required, StringLength(128)] public string WrappedUmk { get; set; } = "";
}

public sealed class CompleteE2eePasskeyUnlockDto
{
    public Guid ChallengeId { get; set; }
    /// <summary>仅 assertion 公开材料，不得包含 PRF 输出。</summary>
    public JsonElement Credential { get; set; }
}

public sealed class RenameE2eePasskeyDto
{
    [Required, StringLength(100, MinimumLength = 1)] public string Name { get; set; } = "";
}

public sealed class E2eePasskeyWrapDto
{
    public string CredentialId { get; set; } = "";
    public string RpId { get; set; } = "";
    public string PrfSalt { get; set; } = "";
    public string WrappedUmk { get; set; } = "";
    public int KeyVersion { get; set; }
}

/// <summary>当前用户的 E2EE 通行密钥，仅辅助解锁，不签发账号会话。</summary>
public interface IE2eePasskeyService : IApplicationService
{
    Task<E2eePasskeyConfigurationDto> GetConfigurationAsync();
    Task<List<E2eePasskeyDto>> GetListAsync();
    Task<E2eePasskeyOptionsDto> PostRegistrationOptionsAsync();
    Task<E2eePasskeyDto> PostRegistrationAsync(CompleteE2eePasskeyRegistrationDto input);
    Task<E2eePasskeyOptionsDto> PostUnlockOptionsAsync(Guid id);
    Task<E2eePasskeyWrapDto> PostUnlockAsync(CompleteE2eePasskeyUnlockDto input);
    Task<E2eePasskeyDto> PutNameAsync(Guid id, RenameE2eePasskeyDto input);
    Task DeleteAsync(Guid id);
}
