using System.Runtime.CompilerServices;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;

namespace OmpOS.Api.Cloud;

/// <summary>
/// The engine session of one run of a cloud agent. It opens when the run calls <see cref="CreateAsync"/>: it asks the runtime to wake the worker and find
/// its endpoint, connects an <see cref="AcpWebSocketTransport"/> to the worker's bridge with a fresh worker token, and resumes the agent's earlier engine
/// session with <c>session/load</c> (so a follow-up prompt keeps its conversation, also after an API restart or a worker sleep); only when there is none,
/// or the worker no longer has it, does it start a new one. Every event passes through <see cref="CloudRedactingEventFilter"/> before the caller sees it.
/// The model, option and steering interfaces of the engine session are forwarded, so the session behaves like a local one.
/// </summary>
internal sealed partial class CloudAgentHarnessSession : IOmpSession, IOmpSessionIdentity
{
    private static readonly Action<ILogger, string, Exception?> ResumeFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(72, "CloudResumeFailed"), "The worker could not resume the engine session of cloud agent {AgentId}; a new engine session was started.");

    private readonly CloudAgentRuntime _runtime;
    private readonly WorkerTokenIssuer _tokens;
    private readonly string _sessionId;
    private readonly IPermissionResolver _resolver;
    private readonly AcpApprovalPolicy _policy;
    private readonly TimeSpan _decisionTimeout;
    private readonly ILoggerFactory _loggers;
    private readonly System.Threading.Lock _gate = new();
    private CloudAgentAttachment? _attachment;
    private IOmpSession? _inner;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="CloudAgentHarnessSession"/> class.</summary>
    /// <param name="runtime">The runtime that prepares the worker.</param>
    /// <param name="tokens">The issuer of worker tokens.</param>
    /// <param name="sessionId">The session of the run; the agent is found from it.</param>
    /// <param name="resolver">Decides the permission requests the approval policy admits.</param>
    /// <param name="policy">The approval policy.</param>
    /// <param name="decisionTimeout">How long an admitted permission request waits for a decision.</param>
    /// <param name="loggers">The logger factory.</param>
    public CloudAgentHarnessSession(
        CloudAgentRuntime runtime,
        WorkerTokenIssuer tokens,
        string sessionId,
        IPermissionResolver resolver,
        AcpApprovalPolicy policy,
        TimeSpan decisionTimeout,
        ILoggerFactory loggers)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(loggers);
        _runtime = runtime;
        _tokens = tokens;
        _sessionId = sessionId;
        _resolver = resolver;
        _policy = policy;
        _decisionTimeout = decisionTimeout;
        _loggers = loggers;
    }

    /// <inheritdoc />
    public HarnessState State => Inner?.State ?? (_disposed ? HarnessState.Disposed : HarnessState.Disconnected);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> AdvertisedCapabilities => Inner?.AdvertisedCapabilities ?? new Dictionary<string, string>(StringComparer.Ordinal);

    /// <inheritdoc />
    public string? NativeSessionId => (Inner as IOmpSessionIdentity)?.NativeSessionId;

    private IOmpSession? Inner
    {
        get
        {
            lock (_gate)
            {
                return _inner;
            }
        }
    }

    /// <inheritdoc />
    public async Task CreateAsync(CancellationToken cancellationToken)
    {
        var attachment = await AttachAsync(cancellationToken).ConfigureAwait(false);
        if (attachment.Agent.NativeSessionId is { } nativeId)
        {
            var resumable = BuildInner(attachment);
            try
            {
                await resumable.ResumeAsync(nativeId, cancellationToken).ConfigureAwait(false);
                Publish(resumable);
                return;
            }
            catch (AcpRequestFailedHarnessException)
            {
                // The worker has no such engine session (its volume was replaced, say): this agent starts a new one.
                ResumeFailed(_loggers.CreateLogger<CloudAgentHarnessSession>(), attachment.Agent.Id, null);
                await resumable.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                await resumable.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        var fresh = BuildInner(attachment);
        try
        {
            await fresh.CreateAsync(cancellationToken).ConfigureAwait(false);
            await ApplyHintsAsync(fresh, attachment, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await fresh.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        Publish(fresh);
        if ((fresh as IOmpSessionIdentity)?.NativeSessionId is { } createdId)
        {
            await _runtime.RecordNativeSessionAsync(attachment.Agent.Id, createdId).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var attachment = await AttachAsync(cancellationToken).ConfigureAwait(false);
        var inner = BuildInner(attachment);
        try
        {
            await inner.ResumeAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        Publish(inner);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);
        var inner = Inner ?? throw new InvalidOperationException("The cloud agent session is not open.");
        var filter = new CloudRedactingEventFilter(_attachment!.Redactor);
        await foreach (var harnessEvent in inner.SubmitPromptAsync(prompt, options, cancellationToken).ConfigureAwait(false))
        {
            foreach (var cleaned in filter.Process(harnessEvent))
            {
                yield return cleaned;
            }
        }

        foreach (var tail in filter.Flush())
        {
            yield return tail;
        }
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken) => Inner?.CancelAsync(cancellationToken) ?? Task.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IOmpSession? inner;
        CloudAgentAttachment? attachment;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            inner = _inner;
            attachment = _attachment;
        }

        if (inner is not null)
        {
            await inner.DisposeAsync().ConfigureAwait(false);
        }

        await _runtime.RunEndedAsync(_sessionId, attachment?.Agent.Id).ConfigureAwait(false);
    }

    private async Task<CloudAgentAttachment> AttachAsync(CancellationToken cancellationToken)
    {
        try
        {
            var attachment = await _runtime.AttachAsync(_sessionId, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                _attachment = attachment;
            }

            return attachment;
        }
        catch (CloudProvisioningException ex)
        {
            // The message is OmpOS-written; the code travels in the context so a log can tell why the run did not start.
            throw new ProcessStartHarnessException(ex.Message, ex.Code);
        }
    }

    private IOmpSession BuildInner(CloudAgentAttachment attachment)
    {
        var tokens = _tokens;
        var sessionId = _sessionId;
        var loggers = _loggers;
        var client = new OmpAcpClient(
            acpOptions => new AcpWebSocketTransport(
                new AcpWebSocketTransportOptions
                {
                    Endpoint = attachment.Endpoint,
                    BearerTokenProvider = _ => ValueTask.FromResult(tokens.Issue(sessionId)),
                },
                loggers.CreateLogger<AcpWebSocketTransport>()),
            _resolver,
            _decisionTimeout,
            loggers);
        return client.CreateSession(new AcpClientOptions
        {
            AuthMethodId = AcpClientOptions.OmpAuthMethodId,
            HandshakeTimeout = AcpClientOptions.OmpHandshakeTimeout,
            WorkingDirectory = attachment.WorkingDirectory,
            ApprovalPolicy = _policy,
        });
    }

    private void Publish(IOmpSession inner)
    {
        lock (_gate)
        {
            _inner = inner;
        }

        if (inner is IOmpModelControl model)
        {
            model.ModelChanged += OnModelChanged;
        }

        if (inner is IOmpSessionOptions options)
        {
            options.OptionsChanged += OnOptionsChanged;
        }

        if (inner is IOmpSessionCommands commands)
        {
            commands.CommandsChanged += OnCommandsChanged;
        }
    }

    private static async Task ApplyHintsAsync(IOmpSession session, CloudAgentAttachment attachment, CancellationToken cancellationToken)
    {
        // The model and mode are hints given when the agent was created. The engine may not offer them; a refusal is not an error of the run.
        if (attachment.Agent.Model is { } model && session is IOmpModelControl modelControl)
        {
            _ = await modelControl.SetModelAsync(model, cancellationToken).ConfigureAwait(false);
        }

        if (attachment.Agent.Mode is { } mode && session is IOmpSessionOptions options)
        {
            _ = await options.SetOptionAsync("mode", mode, cancellationToken).ConfigureAwait(false);
        }
    }
}
