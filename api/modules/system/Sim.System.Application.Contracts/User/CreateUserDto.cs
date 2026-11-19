using System.ComponentModel.DataAnnotations;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.Contracts.User;

public class CreateUserDto : EntityDto<Guid>
{
    [Required]
    [StringLength(28)]
    public required string UserName { get; set; }

    /// <summary>
    /// 仅自助注册使用；管理端创建用户时忽略该字段，统一使用系统配置的用户默认密码。
    /// </summary>
    [StringLength(28)]
    public string? Password { get; set; }

    //User Profile
    [Required]
    [StringLength(20)]
    public required string NickName { get; set; }

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
    /// 创建后分配给该用户的角色；为空时不分配角色。
    /// </summary>
    public List<Guid>? RoleIds { get; set; }
}