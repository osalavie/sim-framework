using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Sim.Framework.Application.Content;

namespace Sim.Framework.Application.ModelBinding;

/// <summary>
/// Binds <see cref="Contracts.Content.IRemoteStreamContent"/> action parameters
/// from a multipart/form-data file. Replaces Volo.Abp.AspNetCore.Mvc.ContentFormFileModelBinder.
/// </summary>
public sealed class RemoteStreamContentModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var request = bindingContext.HttpContext.Request;
        if (!request.HasFormContentType)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        var form = await request.ReadFormAsync(bindingContext.HttpContext.RequestAborted);
        var files = form.Files;
        IFormFile? file = null;

        // 1) match by parameter name (the C# parameter — e.g. "file")
        if (!string.IsNullOrEmpty(bindingContext.FieldName))
        {
            file = files.GetFile(bindingContext.FieldName);
        }

        // 2) fallback: take the first file when the parameter is named "file"
        //    (matches the frontend upload helper that appends 'file' to FormData)
        if (file == null && string.Equals(bindingContext.FieldName, "file", StringComparison.Ordinal))
        {
            file = files.FirstOrDefault();
        }

        if (file == null || file.Length == 0)
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.FieldName ?? "file",
                "A non-empty file is required.");
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        var content = new RemoteStreamContent(
            file.OpenReadStream(),
            file.FileName,
            file.ContentType,
            file.Length,
            disposeStream: true);

        bindingContext.Result = ModelBindingResult.Success(content);
    }
}
