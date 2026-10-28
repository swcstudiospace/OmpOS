using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sessions;
using Autonogrammer.Workflows;

namespace Autonogrammer.SessionRuns;

/// <summary>
/// The session-run activities (WFLW-01, OD-02): the single implementation of a prompt run, used by the API's direct path
/// and by any other host that executes session runs.
/// </summary>
/// <remarks>
/// <para>
/// Each prompt is one run: provision a sandbox through <see cref="ISandboxProvider"/>, run the prompt through
/// <see cref="IHarnessSessionFactory"/>, append every mapped harness event to the session log through
/// <see cref="ISessionRepository"/>, settle the turn through <see cref="SessionService"/>, and tear the sandbox down.
/// Every log write and settlement goes through the store, so a run executed in one process is observed by readers in any
/// other process that shares the store.
/// </para>
/// <para>
/// A permission request is routed into the store-backed tool approval. The run waits for its decision for at most the
/// approval timeout (the shared <see cref="PermissionDecisionWindow"/>, 30 seconds); a request that no decision reaches is denied, so the default outcome is deny.
/// </para>
/// <para>
/// Provisioned sandbox ids are tracked per idempotency key because the provider assigns the sandbox id at
/// provision time; teardown resolves it from that mapping.
/// </para>
/// </remarks>
public sealed class SessionRunActivities : ISessionRunActivities
{
    private static readonly TimeSpan ProvisionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long a routed permission request waits for its decision. It is the window the ACP session also uses, so both sides end the wait together.</summary>
    private static readonly TimeSpan DefaultToolApprovalTimeout = PermissionDecisionWindow.Default;

    private static readonly Action<ILogger, string, string, Exception?> SettleSkipped =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1, "PromptSettleSkipped"),
            "Prompt settle skipped for session {SessionId}: {ErrorCode}");

    private static readonly Action<ILogger, string, string, Exception?> SettleFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(2, "PromptSettleFailed"),
            "Prompt settle failed for session {SessionId}: {ErrorType}");

    private readonly ConcurrentDictionary<string, string> _sandboxIds = new(StringComparer.Ordinal);
    private readonly ISandboxProvider _sandbox;
    private readonly IHarnessSessionFactory _factory;
    private readonly ISessionRepository _repository;
    private readonly SessionService _sessions;
    private readonly TimeProvider _time;
    private readonly ISessionRunObserver _observer;
    private readonly TimeSpan _toolApprovalTimeout;
    private readonly ILogger<SessionRunActivities> _logger;

    /// <summary>Initializes a new instance of the <see cref="SessionRunActivities"/> class.</summary>
    /// <param name="sandbox">The sandbox provider (the fake in the default configuration).</param>
    /// <param name="factory">The harness session factory.</param>
    /// <param name="repository">The session store.</param>
    /// <param name="sessions">The session service (admission, approval and completion rules).</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="observer">Receives the log-changed and provisioning signals of the hosting process.</param>
    /// <param name="logger">The logger.</param>
    public SessionRunActivities(
        ISandboxProvider sandbox,
        IHarnessSessionFactory factory,
        ISessionRepository repository,
        SessionService sessions,
        TimeProvider timeProvider,
        ISessionRunObserver observer,
        ILogger<SessionRunActivities> logger)
        : this(sandbox, factory, repository, sessions, timeProvider, observer, logger, DefaultToolApprovalTimeout)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SessionRunActivities"/> class with an explicit approval timeout.</summary>
    /// <param name="sandbox">The sandbox provider.</param>
    /// <param name="factory">The harness session factory.</param>
    /// <param name="repository">The session store.</param>
    /// <param name="sessions">The session service.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="observer">The run observer.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="toolApprovalTimeout">How long a routed permission request waits for its decision. Must be positive.</param>
    internal SessionRunActivities(
        ISandboxProvider sandbox,
        IHarnessSessionFactory factory,
        ISessionRepository repository,
        SessionService sessions,
        TimeProvider timeProvider,
        ISessionRunObserver observer,
        ILogger<SessionRunActivities> logger,
        TimeSpan toolApprovalTimeout)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(logger);
        if (toolApprovalTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(toolApprovalTimeout), toolApprovalTimeout, "The tool approval timeout must be positive.");
        }

        _sandbox = sandbox;
        _factory = factory;
        _repository = repository;
        _sessions = sessions;
        _time = timeProvider;
        _observer = observer;
        _toolApprovalTimeout = toolApprovalTimeout;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SandboxDescriptor> ProvisionSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stopwatch = Stopwatch.StartNew();
        var request = new ProvisionRequest
        {
            IdempotencyKey = input.IdempotencyKey,
            SessionId = input.SessionId,
            Timeout = ProvisionTimeout,
            Isolation = Isolation.Process,
            NetworkPolicy = NetworkPolicy.DefaultDeny,
            Mounts = [],
            SecretRefs = [],
            Spec = new SandboxSpec
            {
                Image = "autonogrammer/in-process",
                Command = ["prompt"],
                WorkingDirectory = "/",
                Environment = new Dictionary<string, string>(StringComparer.Ordinal),
                Limits = null,
            },
        };

        var result = await _sandbox.ProvisionAsync(request, cancellationToken).ConfigureAwait(false);
        _sandboxIds[input.IdempotencyKey] = result.Descriptor.Id;
        _observer.SandboxProvisioned(stopwatch.Elapsed.TotalMilliseconds);
        return result.Descriptor;
    }

    /// <inheritdoc />
    public async Task<long> RunPromptAsync(SessionRunInput input, Action<string> heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(heartbeat);

        try
        {
            string? stopReason = null;
            var sawError = false;
            var appended = 0;
            var harness = _factory.CreateSession(input.Prompt, input.SessionId, input.RunId);
            await using (harness.ConfigureAwait(false))
            {
                await harness.CreateAsync(cancellationToken).ConfigureAwait(false);
                await foreach (var evt in harness.SubmitPromptAsync(input.Prompt, new PromptOptions { Steer = false }, cancellationToken).ConfigureAwait(false))
                {
                    if (evt is CompletedEvent completed)
                    {
                        // The completion settles the turn; RecordCompletionAsync writes the turn's terminal event.
                        stopReason = completed.Reason;
                        break;
                    }

                    if (evt is PermissionRequestEvent permission)
                    {
                        // The request is held in approval and the run waits for its decision; no other event streams meanwhile.
                        // The store keeps request identifiers unique per session for its whole life, so the run scopes its identifier
                        // to the run: a call identifier reused by a later turn cannot collide with an earlier request.
                        var requestId = $"{input.RunId}:{permission.RequestId ?? permission.CallId}";
                        await AwaitToolApprovalAsync(input, requestId, permission.Name, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    sawError |= evt is ErrorEvent;
                    await AppendAsync(input.SessionId, input.CorrelationId, HarnessEventMapper.ToPayload(evt), cancellationToken).ConfigureAwait(false);
                    appended++;
                    heartbeat($"appended:{appended}");
                }
            }

            var final = HarnessTurnOutcome.SettledStatus(stopReason, sawError);
            await _sessions.RecordCompletionAsync(input.SessionId, final, CancellationToken.None, stopReason).ConfigureAwait(false);
            _observer.SessionChanged(input.SessionId);
        }
        catch (OperationCanceledException)
        {
            await SettleAsync(input.SessionId, SessionStatus.Cancelled).ConfigureAwait(false);
            throw;
        }
#pragma warning disable CA1031 // The activities boundary settles the session as failed, then rethrows so the runner records the failure.
        catch (Exception)
        {
            await SettleAsync(input.SessionId, SessionStatus.Failed).ConfigureAwait(false);
            throw;
        }
#pragma warning restore CA1031

        var session = await _repository.GetAsync(input.SessionId, CancellationToken.None).ConfigureAwait(false);
        return session is null || session.Events.Count == 0 ? 0 : session.Events[^1].Seq;
    }

    /// <summary>
    /// Routes a permission request into the session's store-backed tool approval and waits for its decision. A request that
    /// no decision reaches within the approval timeout is denied by the session service.
    /// </summary>
    private async Task AwaitToolApprovalAsync(SessionRunInput input, string requestId, string toolName, CancellationToken cancellationToken)
    {
        try
        {
            await _sessions.RequestToolApprovalAsync(input.SessionId, requestId, toolName, cancellationToken).ConfigureAwait(false);
        }
        catch (SessionException)
        {
            // Registration is refused when the request is already stored: the resolver settled it at the registration bound, or is
            // settling it now. The run neither registers nor decides it again, and the wait below reads the stored decision.
            // A refusal for any other reason is rethrown.
            if (await _repository.GetToolApprovalAsync(input.SessionId, requestId, cancellationToken).ConfigureAwait(false) is null)
            {
                throw;
            }
        }
        _observer.SessionChanged(input.SessionId);
        _ = await _sessions.WaitForDecisionAsync(input.SessionId, requestId, _toolApprovalTimeout, cancellationToken).ConfigureAwait(false);
        _observer.SessionChanged(input.SessionId);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> CollectArtifactsAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <inheritdoc />
    public async Task TeardownSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_sandboxIds.TryGetValue(input.IdempotencyKey, out var sandboxId))
        {
            return;
        }

        // The mapping stays until the provider confirms teardown, so a failed attempt can be retried by the caller.
        await _sandbox.TeardownAsync(sandboxId, cancellationToken).ConfigureAwait(false);
        _ = _sandboxIds.TryRemove(new KeyValuePair<string, string>(input.IdempotencyKey, sandboxId));
    }

    private async Task AppendAsync(string sessionId, string correlationId, StreamEventPayload payload, CancellationToken cancellationToken)
    {
        var evt = new StreamEvent
        {
            Seq = 0,
            SessionId = sessionId,
            CorrelationId = correlationId,
            Timestamp = _time.GetUtcNow(),
            Payload = payload,
        };
        await _repository.AppendAsync(sessionId, evt, cancellationToken).ConfigureAwait(false);
        _observer.SessionChanged(sessionId);
    }

    private async Task SettleAsync(string sessionId, SessionStatus final)
    {
        try
        {
            await _sessions.RecordCompletionAsync(sessionId, final, CancellationToken.None).ConfigureAwait(false);
            _observer.SessionChanged(sessionId);
        }
        catch (SessionException ex)
        {
            SettleSkipped(_logger, sessionId, ex.Error.Code.ToString(), null);
        }
#pragma warning disable CA1031 // Settle is the last-resort boundary; its own failure must be logged, never thrown.
        catch (Exception ex)
        {
            SettleFailed(_logger, sessionId, ex.GetType().FullName ?? "Unknown", null);
        }
#pragma warning restore CA1031
    }
}
