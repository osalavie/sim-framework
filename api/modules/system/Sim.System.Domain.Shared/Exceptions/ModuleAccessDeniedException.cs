using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Domain.Shared.Exceptions;

/// <summary>
/// Exception thrown when a user does not have permission to access a module
/// HTTP Status: 403 Forbidden
/// </summary>
public class ModuleAccessDeniedException : SystemModuleException
{
    public ModuleAccessDeniedException(Guid moduleId, string moduleName)
        : base(code: SystemErrorCodes.ModuleAccessDenied)
    {
        WithData("ModuleId", moduleId);
        WithData("ModuleName", moduleName);
    }

    public ModuleAccessDeniedException(string moduleName)
        : base(code: SystemErrorCodes.ModuleAccessDenied)
    {
        WithData("ModuleName", moduleName);
    }
}
