using Microsoft.AspNetCore.Mvc.ModelBinding;
using Sim.Framework.Application.Contracts.Content;

namespace Sim.Framework.Application.ModelBinding;

/// <summary>
/// Activates <see cref="RemoteStreamContentModelBinder"/> for
/// <see cref="IRemoteStreamContent"/> action parameters.
/// </summary>
public sealed class RemoteStreamContentModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (typeof(IRemoteStreamContent).IsAssignableFrom(context.Metadata.ModelType))
        {
            return new RemoteStreamContentModelBinder();
        }

        return null;
    }
}
