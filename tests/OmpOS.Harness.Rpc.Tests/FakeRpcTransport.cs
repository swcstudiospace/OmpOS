using System.Threading.Channels;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>
/// Scripted <see cref="IRpcTransport"/>. Lines emitted by the test (or by <c>respond</c> for each command written)
/// are delivered to the session's pump exactly as stdout would deliver them. Every written command is recorded.
/// </summary>
internal sealed class FakeRpcTransport : IRpcTransport
{
    private readonly Channel<string> _stdout = Channel.CreateUnbounded<string>();
    private readonly Func<string, IEnumerable<string>>? _respond;
    private readonly List<string> _sent = [];
    private readonly System.Threading.Lock _gate = new();

    /// <summary>Creates a transport that answers each written command with <paramref name="respond"/>'s lines.</summary>
    /// <param name="respond">Maps a written command line to the stdout lines it produces; null produces none.</param>
    public FakeRpcTransport(Func<string, IEnumerable<string>>? respond = null)
    {
        _respond = respond;
    }

    /// <summary>Gets the command lines written so far, oldest first.</summary>
    public IReadOnlyList<string> Sent
    {
        get
        {
            lock (_gate)
            {
                return _sent.ToArray();
            }
        }
    }

    /// <summary>Gets whether the session closed stdin.</summary>
    public bool InputClosed { get; private set; }

    /// <summary>Delivers one stdout line to the session.</summary>
    /// <param name="line">The frame line.</param>
    public void Emit(string line) => _stdout.Writer.TryWrite(line);

    /// <summary>Ends stdout, as process exit would.</summary>
    public void EndStdout() => _stdout.Writer.TryComplete();

    /// <inheritdoc />
    public IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken) =>
        _stdout.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        IEnumerable<string> replies;
        lock (_gate)
        {
            _sent.Add(line);
            replies = _respond?.Invoke(line) ?? [];
        }

        foreach (string reply in replies)
        {
            Emit(reply);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CloseInputAsync(CancellationToken cancellationToken)
    {
        InputClosed = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        EndStdout();
        return ValueTask.CompletedTask;
    }
}
