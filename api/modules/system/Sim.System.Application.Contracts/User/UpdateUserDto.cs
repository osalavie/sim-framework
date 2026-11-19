using System.ComponentModel.DataAnnotations;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.Contracts.User;

public class UpdateUserDto : EntityDto<Guid>
{
    [Required]
    [StringLength(128)]
    public string UserName { get; set; } = string.Empty;

    //User Profile
    [Required]
    [StringLength(20)]
    public string NickName { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public string? Name { get; set; }

    public SexEnum Sex { get; set; }

    public string? Phone { get; set; }

    private string? _email;

    [EmailAddress]
    public string? Email
    {
        get => _email;
        set => _email = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string? Address { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// 覆盖式同步该用户的角色；为 null 时保持现有角色不变。
    /// </summary>
    public List<Guid>? RoleIds { get; set; }
}