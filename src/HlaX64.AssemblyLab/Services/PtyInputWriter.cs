using System.Threading.Channels;

namespace HlaX64.AssemblyLab.Services;

/// <summary>Ordered, non-blocking writer for interactive PTY stdin.</summary>
internal sealed class PtyInputWriter : IDisposable
{
    private readonly Channel<byte[]> _channel = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly CancellationTokenSource _cts = new();
    private InteractivePtySession? _session;
    private Task? _pump;

    public void Attach(InteractivePtySession session)
    {
        _session = session;
        _pump ??= Task.Run(PumpAsync);
    }

    public void Detach() => _session = null;

    public void Enqueue(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        _channel.Writer.TryWrite(data.ToArray());
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var chunk in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
                _session?.Send(chunk);
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _pump?.Wait(TimeSpan.FromSeconds(1));
        _cts.Dispose();
    }
}
