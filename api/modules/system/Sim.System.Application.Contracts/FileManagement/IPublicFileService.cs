using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.FileManagement;

public interface IPublicFileService : IApplicationService
{
    Task<IRemoteStreamContent> GetAvatarAsync(
        string key,
        string provider,
        CancellationToken cancellationToken = default);
}
