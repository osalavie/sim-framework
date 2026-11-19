using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Domain.Shared.Exceptions;

/// <summary>
/// Exception thrown when module data validation fails
/// HTTP Status: 400 Bad Request
/// </summary>
public class InvalidModuleDataException : SystemModuleException
{
    public InvalidModuleDataException(string message)
        : base(code: SystemErrorCodes.InvalidModuleData)
    {
        WithData("FieldName", "Module");
        WithData("Reason", message);
    }

    public InvalidModuleDataException(string fieldName, string reason)
        : base(code: SystemErrorCodes.InvalidModuleData)
    {
        WithData("FieldName", fieldName);
        WithData("Reason", reason);
    }
}
