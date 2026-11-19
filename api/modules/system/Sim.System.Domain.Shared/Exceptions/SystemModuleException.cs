using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.System.Domain.Shared.Exceptions;

/// <summary>
/// Base exception for System module
/// </summary>
public class SystemModuleException : BusinessException
{
    public SystemModuleException(string code, string? message = null)
        : base(code, message ?? code)
    {
    }
}
