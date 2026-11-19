using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sim.Framework.Application.Content;
using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.FileManagement;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Application.FileManagement;

[AllowAnonymous]
public class PublicFileService(IFileStorage storage) : ApplicationService, IPublicFileService
{
    /// <summary>
    /// 读取公开头像文件。
    /// </summary>
    [HttpGet]
    [RateLimit]
    public async Task<IRemoteStreamContent> GetAvatarAsync(
        string key,
        string provider,
        CancellationToken cancellationToken = default)
    {
        if (!AvatarStorageKey.IsPublicAvatar(key))
        {
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }

        var result = await storage.OpenReadAsync(key, provider, cancellationToken);
        if (result == null)
        {
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }

        return new RemoteStreamContent(
            result.Content,
            result.FileName,
            result.ContentType,
            result.Length);
    }
}
