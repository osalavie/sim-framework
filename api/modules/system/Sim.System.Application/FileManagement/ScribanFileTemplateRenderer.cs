using Scriban;
using Sim.System.FileManagement;
using Scriban.Runtime;

namespace Sim.System.Application.FileManagement;

public sealed class ScribanFileTemplateRenderer : IFileTemplateRenderer
{
    public async Task<string> RenderAsync(
        string template,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        cancellationToken.ThrowIfCancellationRequested();

        var parsed = Template.Parse(template);
        if (parsed.HasErrors)
        {
            throw new FormatException(string.Join(Environment.NewLine, parsed.Messages));
        }

        var globals = new ScriptObject();
        foreach (var (key, value) in model)
        {
            globals[key] = value;
            globals.SetReadOnly(key, true);
        }

        var context = new TemplateContext
        {
            StrictVariables = true,
            LoopLimit = 10_000,
            RecursiveLimit = 64
        };
        context.PushGlobal(globals);
        cancellationToken.ThrowIfCancellationRequested();
        return await parsed.RenderAsync(context);
    }
}
