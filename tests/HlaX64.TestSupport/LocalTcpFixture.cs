using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HlaX64.TestSupport;

public sealed class LocalTcpFixture : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Task _serverTask;
    private readonly CancellationTokenSource _cts;

    public int Port { get; }

    private LocalTcpFixture(TcpListener listener, Task serverTask, CancellationTokenSource cts, int port)
    {
        _listener = listener;
        _serverTask = serverTask;
        _cts = cts;
        Port = port;
    }

    public static LocalTcpFixture? TryStart(string toolDir)
    {
        var serverPath = Path.Combine(toolDir, "expected.server");
        if (!File.Exists(serverPath))
            return null;

        var template = File.ReadAllText(serverPath).Replace("\r\n", "\n").Replace("`n", "\n");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var echoMode = template.Trim() == "$ECHO";
        var slowMode = template.Trim() == "$SLOW";
        var noResponseMode = template.Trim() == "$NORESP";

        var cts = new CancellationTokenSource();
        var serverTask = Task.Run(async () =>
        {
            using var reg = cts.Token.Register(() => listener.Stop());
            using var client = await listener.AcceptTcpClientAsync(cts.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();
            if (slowMode)
            {
                await Task.Delay(5000, cts.Token).ConfigureAwait(false);
                return;
            }

            if (noResponseMode)
            {
                var buffer = new byte[4096];
                _ = await stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
                return;
            }

            var buffer2 = new byte[4096];
            var read = await stream.ReadAsync(buffer2, cts.Token).ConfigureAwait(false);
            if (echoMode)
            {
                if (read > 0)
                    await stream.WriteAsync(buffer2.AsMemory(0, read), cts.Token).ConfigureAwait(false);
            }
            else
            {
                var bytes = Encoding.ASCII.GetBytes(template);
                await stream.WriteAsync(bytes, cts.Token).ConfigureAwait(false);
            }
        }, cts.Token);

        return new LocalTcpFixture(listener, serverTask, cts, port);
    }

    public void WaitForCompletion()
    {
        if (!_serverTask.Wait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException($"Local TCP fixture on port {Port} did not complete within 10s.");
        if (_serverTask.IsFaulted)
            throw new InvalidOperationException(_serverTask.Exception?.GetBaseException().Message, _serverTask.Exception);
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { /* best effort */ }
        try { _listener.Stop(); } catch { /* best effort */ }
        try { _serverTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* best effort */ }
        _cts.Dispose();
    }
}
