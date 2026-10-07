using System.IO.Pipes;
using System.Text;
using Serilog;

namespace GrowthTracker.Desktop.Services;

/// <summary>
/// One app instance per user. A second launch (e.g. from the Start menu while the app sits in the tray) asks
/// the running instance to show its window and exits, so there are never two processes firing reminders.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string ActivateMessage = "activate";
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex, bool isFirst, string pipeName)
    {
        _mutex = mutex;
        IsFirst = isFirst;
        _pipeName = pipeName;
    }

    public bool IsFirst { get; }

    public static SingleInstance Acquire(string appId = "GrowthTracker")
    {
        string id = appId + "." + new string(Environment.UserName.Where(char.IsLetterOrDigit).ToArray());
        var mutex = new Mutex(initiallyOwned: true, @"Local\" + id, out bool createdNew);
        return new SingleInstance(mutex, createdNew, id + ".activate");
    }

    /// <summary>Second instance: tells the first one to show itself. Returns false if it could not be reached.</summary>
    public bool SignalFirstInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(2000);
            var bytes = Encoding.UTF8.GetBytes(ActivateMessage);
            client.Write(bytes, 0, bytes.Length);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not reach the running instance");
            return false;
        }
    }

    /// <summary>First instance: calls <paramref name="onActivate"/> (on a background thread) whenever a second launch signals.</summary>
    public void Listen(Action onActivate) => _ = Task.Run(async () =>
    {
        var buffer = new byte[64];
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                                                                   PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_stop.Token);
                int read = await server.ReadAsync(buffer, _stop.Token);
                if (Encoding.UTF8.GetString(buffer, 0, read) == ActivateMessage)
                    onActivate();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Single-instance listener error");
                await Task.Delay(1000);
            }
        }
    });

    public void Dispose()
    {
        _stop.Cancel();
        if (IsFirst)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
