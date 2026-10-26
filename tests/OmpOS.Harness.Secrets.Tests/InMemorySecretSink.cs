namespace OmpOS.Harness.Secrets.Tests;

/// <summary>
/// In-memory sink for one channel. It records every write in order and refuses a write addressed to another channel.
/// </summary>
internal sealed class InMemorySecretSink : ISecretSink
{
    private readonly Lock _sync = new();
    private readonly List<string> _writes = [];
    private readonly SecretSinkChannel _channel;

    /// <summary>Creates a sink that records writes for one channel.</summary>
    /// <param name="channel">The only channel this sink accepts.</param>
    public InMemorySecretSink(SecretSinkChannel channel)
    {
        _channel = channel;
    }

    /// <summary>The channel this sink records.</summary>
    public SecretSinkChannel Channel => _channel;

    /// <summary>Every text written so far, in order.</summary>
    public IReadOnlyList<string> Writes
    {
        get
        {
            lock (_sync)
            {
                return _writes.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public Task WriteAsync(SecretSinkChannel channel, string text, CancellationToken cancellationToken)
    {
        if (text is null)
        {
            return Task.FromException(new ArgumentNullException(nameof(text)));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        if (channel != _channel)
        {
            return Task.FromException(
                new InvalidOperationException("This sink records a single channel and refuses writes to any other."));
        }

        lock (_sync)
        {
            _writes.Add(text);
        }

        return Task.CompletedTask;
    }
}
