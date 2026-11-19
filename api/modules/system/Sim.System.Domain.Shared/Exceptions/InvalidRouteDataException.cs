using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Domain.Shared.Exceptions;

/// <summary>
/// Exception thrown when route data validation fails
/// HTTP Status: 400 Bad Request
/// </summary>
public class InvalidRouteDataException : SystemModuleException
{
    public InvalidRouteDataException(string message)
        : base(code: SystemErrorCodes.InvalidRouteData)
    {
        WithData("FieldName", "Route");
        WithData("Reason", message);
    }

    public InvalidRouteDataException(string fieldName, string reason)
        : base(code: SystemErrorCodes.InvalidRouteData)
    {
        WithData("FieldName", fieldName);
        WithData("Reason", reason);
    }
}
