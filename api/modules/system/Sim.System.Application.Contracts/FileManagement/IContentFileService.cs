using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.FileManagement;

public interface IContentFileService : IApplicationService
{
    Task<ContentFileDto> UploadAsync(
        IRemoteStreamContent file,
        CancellationToken cancellationToken = default);

    Task<IRemoteStreamContent> GetAsync(
        Guid id,
        string? shareToken = null,
        CancellationToken cancellationToken = default);
}
