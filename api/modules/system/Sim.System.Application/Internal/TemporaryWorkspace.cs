namespace Sim.System.Application.Internal;

internal sealed class TemporaryWorkspace : IDisposable
{
    public TemporaryWorkspace(string rootPath)
    {
        Directory.CreateDirectory(rootPath);
        DirectoryPath = Path.Combine(rootPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public string Combine(string relativePath) => Path.Combine(DirectoryPath, relativePath);

    public void Dispose()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (IOException)
        {
            // The OS may keep a short-lived handle after an external process exits.
        }
        catch (UnauthorizedAccessException)
        {
            // A background cleanup job may remove an orphaned workspace later.
        }
    }
}
