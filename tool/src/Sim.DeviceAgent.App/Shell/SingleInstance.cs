using System.IO.Pipes;

namespace Sim.DeviceAgent.App.Shell;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private CancellationTokenSource? _cts;
    private int _disposed;

    public SingleInstance(string name)
    {
        _pipeName = name;
        _mutex = new Mutex(initiallyOwned: true, @"Local\" + name, out var createdNew);
        IsPrimary = createdNew;
    }

    public bool IsPrimary { get; }

    public void StartServer(Action onShow)
    {
        if (!IsPrimary)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var reader = new StreamReader(server);
                    var command = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (string.Equals(command, "SHOW", StringComparison.Ordinal))
                    {
                        onShow();
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // The next loop iteration recreates the pipe server.
                }
            }
        }, token);
    }

    public void SignalShow()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client);
            writer.WriteLine("SHOW");
            writer.Flush();
        }
        catch
        {
            // The primary instance may still be starting; its window opens normally.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();

        if (IsPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was not owned by this thread.
            }
        }

        _mutex.Dispose();
    }
}
