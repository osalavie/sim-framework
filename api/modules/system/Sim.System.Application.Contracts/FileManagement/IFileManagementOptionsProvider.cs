using Microsoft.Extensions.Options;

namespace Sim.System.FileManagement;

public interface IFileManagementOptionsProvider
{
    Task<FileManagementOptions> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class ConfiguredFileManagementOptionsProvider(
    IOptions<FileManagementOptions> options) : IFileManagementOptionsProvider
{
    public Task<FileManagementOptions> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(options.Value);
    }
}
