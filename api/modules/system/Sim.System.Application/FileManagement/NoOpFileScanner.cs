using Sim.System.FileManagement;

namespace Sim.System.Application.FileManagement;

/// <summary>
/// No-op file scanner used in development environments where ClamAV is not available.
/// Always returns null (clean). Also serves as a fallback when ClamAV is unreachable.
/// </summary>
public sealed class NoOpFileScanner : IFileScanner
{
    public string Name => "NoOp";

    public Task<string?> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
