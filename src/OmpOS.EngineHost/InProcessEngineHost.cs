using System.Runtime.CompilerServices;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Harness.InMemory;

namespace OmpOS.EngineHost;

/// <summary>
/// Desktop host: binds one <see cref="IOmpSession"/> at a time, created by an injectable factory,
/// so tests substitute scripted sessions and never spawn omp.
/// </summary>
public sealed class InProcessEngineHost : IEngineHost, IAsyncDisposable
{
    private readonly Func<IOmpSession> _sessionFactory;
    private readonly System.Threading.Lock _gate = new();
    private IOmpSession? _session;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="InProcessEngineHost"/> class.</summary>
    /// <param name="sessionFactory">Creates the session on first use (and again after a fault).</param>
    /// <param name="descriptor">The descriptor this host reports.</param>
    public InProcessEngineHost(Func<IOmpSession> sessionFactory, EngineHostDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(sessionFactory);
        ArgumentNullException.ThrowIfNull(descriptor);
        _sessionFactory = sessionFactory;
        Descriptor = descriptor;
    }

    /// <inheritdoc />
    public EngineHostDescriptor Descriptor { get; }

    /// <summary>Creates a host over the scripted in-memory adapter. Never spawns a process.</summary>
    /// <param name="adapter">The adapter each session is created from.</param>
    /// <param name="descriptor">The descriptor to report, or the in-process default.</param>
    /// <returns>A host whose sessions replay the adapter's script.</returns>
    public static InProcessEngineHost ForInMemory(InMemoryOmpAdapter adapter, EngineHostDescriptor? descriptor = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        return new InProcessEngineHost(
            adapter.CreateSession,
            descriptor ?? new EngineHostDescriptor { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None });
    }

    /// <summary>Creates a host over a local omp child process via <c>omp acp</c>.</summary>
    /// <param name="client">The ACP client each session is created from.</param>
    /// <param name="options">The session options (notably <c>OmpPath</c>).</param>
    /// <param name="descriptor">The descriptor to report, or the in-process default.</param>
    /// <returns>A host whose sessions speak ACP over stdio.</returns>
    public static InProcessEngineHost ForAcp(OmpAcpClient client, AcpClientOptions options, EngineHostDescriptor? descriptor = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        return new InProcessEngineHost(
            () => client.CreateSession(options),
            descriptor ?? new EngineHostDescriptor { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None });
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
        string prompt,
        PromptOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);
        var session = await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var evt in session.SubmitPromptAsync(prompt, options, cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IOmpSession? session;
        lock (_gate)
        {
            session = _session;
        }

        return session?.CancelAsync(cancellationToken) ?? Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IOmpSession? session;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _session;
            _session = null;
        }

        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<IOmpSession> EnsureSessionAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session is not null && _session.State is not HarnessState.Faulted and not HarnessState.Disposed)
            {
                return _session;
            }
        }

        var fresh = _sessionFactory();
        try
        {
            await fresh.CreateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await fresh.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        IOmpSession winner;
        IOmpSession? loser = null;
        bool disposed;
        lock (_gate)
        {
            disposed = _disposed;
            if (!disposed)
            {
                if (_session is null || _session.State is HarnessState.Faulted or HarnessState.Disposed)
                {
                    winner = _session = fresh;
                }
                else
                {
                    // A concurrent caller installed a live session first; keep it and drop this one.
                    winner = _session;
                    loser = fresh;
                }
            }
            else
            {
                winner = fresh;
                loser = fresh;
            }
        }

        if (disposed)
        {
            await loser!.DisposeAsync().ConfigureAwait(false);
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (loser is not null)
        {
            await loser.DisposeAsync().ConfigureAwait(false);
        }

        return winner;
    }
}
