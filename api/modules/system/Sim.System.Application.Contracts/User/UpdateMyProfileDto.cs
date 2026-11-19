using System.ComponentModel.DataAnnotations;

namespace Sim.System.Application.Contracts.User;

public class UpdateMyProfileDto
{
    [Required]
    [StringLength(20)]
    public string NickName { get; set; } = string.Empty;

    [StringLength(128)]
    public string? Name { get; set; }

    [StringLength(500)]
    public string? Avatar { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }
}
