using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Domain.Shared.Exceptions;

/// <summary>
/// Exception thrown when a module is not found
/// HTTP Status: 404 Not Found
/// </summary>
public class ModuleNotFoundException : SystemModuleException
{
    public ModuleNotFoundException(Guid moduleId)
        : base(code: SystemErrorCodes.ModuleNotFound)
    {
        WithData("ModuleId", moduleId);
        WithData("Identifier", moduleId);
    }

    public ModuleNotFoundException(string moduleName)
        : base(code: SystemErrorCodes.ModuleNotFound)
    {
        WithData("ModuleName", moduleName);
        WithData("Identifier", moduleName);
    }
}
