using System.ComponentModel.DataAnnotations;

namespace Sim.System.Application.Contracts.Role;

public class CreateRoleDto
{
    [Required]
    [StringLength(28)]
    public required string Name { get; set; }

    [Required]
    [StringLength(28)]
    public required string Code { get; set; }

    public string? Description { get; set; }
}