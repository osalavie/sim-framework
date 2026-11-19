using Sim.Framework.Application.Contracts.Dtos;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.Contracts.User;


public class UserDto : AuditedEntityDto<Guid>
{
    public string UserName { get; set; } = string.Empty;

    //User Profile
    public string? NickName { get; set; }

    public string? Avatar { get; set; }

    public string? Name { get; set; }

    public SexEnum Sex { get; set; }

    public string? Phone { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Description { get; set; }

    public bool IsBuiltIn { get; set; }

    public List<RoleInfo> Roles { get; set; } = [];
}
