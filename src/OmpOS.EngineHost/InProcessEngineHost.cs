using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
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
    private readonly HostModelControl _models = new();
    private readonly List<IOmpSession> _opening = [];
    private string? _resumeSessionId;
    private string? _nativeSessionId;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="InProcessEngineHost"/> class.</summary>
    /// <param name="sessionFactory">Creates the session on first use (and again after a fault).</param>
    /// <param name="descriptor">The descriptor this host reports.</param>
    /// <param name="resumeSessionId">
    /// The engine session to resume instead of creating a new one. It applies to the first session this host binds: that session is
    /// opened with <see cref="IOmpSession.ResumeAsync"/>, and every session bound after it is created fresh. Null (the default) creates a new session.
    /// </param>
    public InProcessEngineHost(Func<IOmpSession> sessionFactory, EngineHostDescriptor descriptor, string? resumeSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(sessionFactory);
        ArgumentNullException.ThrowIfNull(descriptor);
        _sessionFactory = sessionFactory;
        Descriptor = descriptor;
        _resumeSessionId = string.IsNullOrWhiteSpace(resumeSessionId) ? null : resumeSessionId;
    }

    /// <summary>
    /// Occurs when the bound session reports a native session identifier that differs from the last one this host reported, which is
    /// the first time the session has been created or resumed. Read <see cref="NativeSessionId"/> for the value. A handler that throws
    /// does not fail the prompt.
    /// </summary>
    public event EventHandler? NativeSessionIdChanged;

    /// <summary>
    /// Gets the identifier the engine gave the bound session, or null until a prompt was submitted or when the session does not
    /// implement <see cref="IOmpSessionIdentity"/>.
    /// </summary>
    public string? NativeSessionId
    {
        get
        {
            lock (_gate)
            {
                return _nativeSessionId;
            }
        }
    }

    /// <inheritdoc />
    public EngineHostDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the model control of the host. It stays valid while the host replaces a faulted session, and its model list is empty until
    /// the first session is bound (see <see cref="ConnectAsync"/>) or when the session's engine offers no model option.
    /// </summary>
    public IOmpModelControl Models => _models;

    /// <summary>
    /// Opens the engine session now, instead of at the first prompt, so its models are known. Does nothing when a live session is
    /// already bound.
    /// </summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A task that completes when the session is open.</returns>
    /// <exception cref="ObjectDisposedException">The host was disposed.</exception>
    public async Task ConnectAsync(CancellationToken cancellationToken) =>
        _ = await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);

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
    /// <param name="resumeSessionId">The engine session the first prompt resumes with <c>session/load</c>, or null to create a new session.</param>
    /// <returns>A host whose sessions speak ACP over stdio.</returns>
    public static InProcessEngineHost ForAcp(OmpAcpClient client, AcpClientOptions options, EngineHostDescriptor? descriptor = null, string? resumeSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        return new InProcessEngineHost(
            () => client.CreateSession(options),
            descriptor ?? new EngineHostDescriptor { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None },
            resumeSessionId);
    }

    /// <summary>
    /// Creates a host over an ACP session whose admitted permission requests are decided by <paramref name="permissionResolver"/>.
    /// A null resolver denies every admitted request, exactly as an <see cref="OmpAcpClient"/> built without one does.
    /// </summary>
    /// <param name="options">The session options, notably the approval policy and <c>OmpPath</c>.</param>
    /// <param name="permissionResolver">Decides the permission requests the approval policy admits. Null denies them.</param>
    /// <param name="descriptor">The descriptor to report, or the in-process default.</param>
    /// <param name="transportFactory">Creates the transport for each session. Null spawns <c>omp acp</c>.</param>
    /// <param name="loggerFactory">Creates the loggers of the sessions and transports. Null discards their log output.</param>
    /// <param name="resumeSessionId">The engine session the first prompt resumes with <c>session/load</c>, or null to create a new session.</param>
    /// <returns>A host whose sessions speak ACP over the transport and answer permission requests through the resolver.</returns>
    public static InProcessEngineHost ForAcp(
        AcpClientOptions options,
        IPermissionResolver? permissionResolver = null,
        EngineHostDescriptor? descriptor = null,
        Func<AcpClientOptions, IAcpTransport>? transportFactory = null,
        ILoggerFactory? loggerFactory = null,
        string? resumeSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var client = transportFactory is null
            ? new OmpAcpClient(permissionResolver, loggerFactory: loggerFactory)
            : new OmpAcpClient(transportFactory, permissionResolver, loggerFactory: loggerFactory);
        return ForAcp(client, options, descriptor, resumeSessionId);
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
        IOmpSession[] opening;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _models.Unbind();
            session = _session;
            _session = null;
            opening = [.. _opening];
            _opening.Clear();
        }

        // A session still opening has its child running already: stop it now instead of when its handshake ends, so no omp outlives the host.
        foreach (var pending in opening)
        {
            await pending.DisposeAsync().ConfigureAwait(false);
        }

        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<IOmpSession> EnsureSessionAsync(CancellationToken cancellationToken)
    {
        if (TryGetLiveSession() is { } live)
        {
            return live;
        }

        var fresh = await CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        var bound = await InstallSessionAsync(fresh).ConfigureAwait(false);
        _models.Bind(bound);
        PublishNativeSessionId(bound);
        return bound;
    }

    /// <summary>Returns the bound session when it can still serve a prompt; throws once the host is disposed.</summary>
    private IOmpSession? TryGetLiveSession()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _session is not null && _session.State is not HarnessState.Faulted and not HarnessState.Disposed
                ? _session
                : null;
        }
    }

    private async Task<IOmpSession> CreateSessionAsync(CancellationToken cancellationToken)
    {
        var fresh = _sessionFactory();
        string? resumeId;
        bool disposed;
        lock (_gate)
        {
            resumeId = _resumeSessionId;
            disposed = _disposed;
            if (!disposed)
            {
                _opening.Add(fresh);
            }
        }

        if (disposed)
        {
            await fresh.DisposeAsync().ConfigureAwait(false);
            throw new ObjectDisposedException(GetType().FullName);
        }

        try
        {
            if (resumeId is null)
            {
                await fresh.CreateAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await fresh.ResumeAsync(resumeId, cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    // The resume succeeded: a session bound after a later fault is created fresh.
                    _resumeSessionId = null;
                }
            }
        }
        catch
        {
            await fresh.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            lock (_gate)
            {
                _ = _opening.Remove(fresh);
            }
        }

        return fresh;
    }

    /// <summary>Records the identifier the bound session reports and raises <see cref="NativeSessionIdChanged"/> when it is new.</summary>
    private void PublishNativeSessionId(IOmpSession session)
    {
        if (session is not IOmpSessionIdentity { NativeSessionId: { Length: > 0 } id })
        {
            return;
        }

        bool changed;
        lock (_gate)
        {
            changed = !string.Equals(_nativeSessionId, id, StringComparison.Ordinal);
            _nativeSessionId = id;
        }

        if (!changed || NativeSessionIdChanged is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A subscriber must never fail the prompt or hide the id from the other subscribers.
            }
        }
    }

    /// <summary>Binds a fresh session unless a concurrent caller bound a live one first, and disposes whatever is displaced or lost.</summary>
    private async Task<IOmpSession> InstallSessionAsync(IOmpSession fresh)
    {
        IOmpSession winner;
        IOmpSession? loser = null;
        IOmpSession? replaced = null;
        bool disposed;
        lock (_gate)
        {
            disposed = _disposed;
            if (!disposed)
            {
                if (_session is null || _session.State is HarnessState.Faulted or HarnessState.Disposed)
                {
                    replaced = _session;
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

        if (replaced is not null)
        {
            // The faulted or disposed engine this one replaces must not stay alive.
            await replaced.DisposeAsync().ConfigureAwait(false);
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
