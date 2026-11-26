using Sim.Framework.Application.Contracts.Dtos;

namespace Sim.System.Application.Contracts.RouteConfig;

/// <summary>
/// Paginated route query parameters: supports filtering by module and keyword
/// </summary>
public class RouteConfigPagedRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// Filter routes by module ID (optional, null means no module filter)
    /// </summary>
    public Guid? ModuleId { get; set; }

    /// <summary>
    /// Search keyword, matches route name, path or menu title (optional)
    /// </summary>
    public string? Keyword { get; set; }
}
