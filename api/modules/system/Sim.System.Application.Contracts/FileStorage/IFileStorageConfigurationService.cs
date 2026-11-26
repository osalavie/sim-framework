using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.FileStorage;

public interface IFileStorageConfigurationService : IApplicationService
{
    Task<FileStorageConfigurationDto> GetAsync(CancellationToken cancellationToken = default);

    Task<FileStorageConfigurationDto> UpdateAsync(
        UpdateFileStorageConfigurationDto input,
        CancellationToken cancellationToken = default);

    Task<FileStorageConnectionTestResultDto> TestAsync(
        TestFileStorageProviderConnectionDto input,
        CancellationToken cancellationToken = default);
}
