using System.Text.Json.Serialization;
using Sim.System.Application.Contracts.Dtos;

namespace Sim.System.Application.Contracts.RouteConfig;

/// <summary>
/// 当前用户的菜单契约：可访问模块与跨模块统一组装的全局路由树。
/// </summary>
public class UserMenuDto
{
    [JsonPropertyName("modules")]
    public List<ModuleConfigDto> Modules { get; set; } = [];

    [JsonPropertyName("routes")]
    public List<RouteConfigDto> Routes { get; set; } = [];
}
