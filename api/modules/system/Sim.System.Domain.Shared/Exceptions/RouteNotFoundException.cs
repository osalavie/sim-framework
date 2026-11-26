using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Domain.Shared.Exceptions;

/// <summary>
/// Exception thrown when a route is not found
/// HTTP Status: 404 Not Found
/// </summary>
public class RouteNotFoundException : SystemModuleException
{
    public RouteNotFoundException(Guid routeId)
        : base(code: SystemErrorCodes.RouteNotFound)
    {
        WithData("RouteId", routeId);
        WithData("Identifier", routeId);
    }

    public RouteNotFoundException(string routeName)
        : base(code: SystemErrorCodes.RouteNotFound)
    {
        WithData("RouteName", routeName);
        WithData("Identifier", routeName);
    }
}
