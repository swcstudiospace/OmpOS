using System.Collections.Concurrent;
using System.Diagnostics;
using OmpOS.Api.Harness;
using OmpOS.Api.Metrics;
using OmpOS.Api.Sse;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sessions;
using OmpOS.Workflows;

namespace OmpOS.Api.Workflows;

/// <summary>
/// The API's <see cref="ISessionRunActivities"/> implementation (WFLW-01).
/// </summary>
/// <remarks>
/// <para>
/// The API is the composition root: this type lives here (not in <c>OmpOS.Workflows</c>) so the
/// workflow assembly never references <see cref="SessionService"/> or the API. Each prompt is one
/// run: provision a sandbox through <see cref="ISandboxProvider"/>, run the prompt through the
/// existing session-service path (same harness stream, same log appends, same SSE fan-out),
/// collect the result, and tear down. <see cref="DirectSessionRunner"/> guarantees teardown runs
/// in a <c>finally</c>, including when the prompt run fails.
/// </para>
/// <para>
/// Provisioned sandbox ids are tracked per idempotency key because the provider assigns the
/// sandbox id at provision time; teardown resolves it from that mapping.
/// </para>
/// </remarks>
internal sealed class ApiSessionRunActivities : ISessionRunActivities
{
    private static readonly TimeSpan ProvisionTimeout = TimeSpan.FromSeconds(30);

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
    private readonly SessionEventStreamer _streamer;
    private readonly TimeProvider _time;
    private readonly ApiMetrics _metrics;
    private readonly ILogger<ApiSessionRunActivities> _logger;

    /// <summary>Initializes a new instance of the <see cref="ApiSessionRunActivities"/> class.</summary>
    /// <param name="sandbox">The sandbox provider (the fake in the default configuration).</param>
    /// <param name="factory">The harness session factory.</param>
    /// <param name="repository">The session store.</param>
    /// <param name="sessions">The session service (admission and completion rules).</param>
    /// <param name="streamer">The SSE broadcaster.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="metrics">The API metrics.</param>
    /// <param name="logger">The logger.</param>
    public ApiSessionRunActivities(
        ISandboxProvider sandbox,
        IHarnessSessionFactory factory,
        ISessionRepository repository,
        SessionService sessions,
        SessionEventStreamer streamer,
        TimeProvider timeProvider,
        ApiMetrics metrics,
        ILogger<ApiSessionRunActivities> logger)
    {
        _sandbox = sandbox;
        _factory = factory;
        _repository = repository;
        _sessions = sessions;
        _streamer = streamer;
        _time = timeProvider;
        _metrics = metrics;
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
                Image = "ompos/in-process",
                Command = ["prompt"],
                WorkingDirectory = "/",
                Environment = new Dictionary<string, string>(StringComparer.Ordinal),
                Limits = null,
            },
        };

        var result = await _sandbox.ProvisionAsync(request, cancellationToken).ConfigureAwait(false);
        _sandboxIds[input.IdempotencyKey] = result.Descriptor.Id;
        _metrics.RecordSandboxProvision(stopwatch.Elapsed.TotalMilliseconds);
        return result.Descriptor;
    }

    /// <inheritdoc />
    public async Task<long> RunPromptAsync(SessionRunInput input, Action<string> heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(heartbeat);

        try
        {
            var appended = 0;
            var harness = _factory.CreateSession(input.Prompt);
            await using (harness.ConfigureAwait(false))
            {
                await harness.CreateAsync(cancellationToken).ConfigureAwait(false);
                await foreach (var evt in harness.SubmitPromptAsync(input.Prompt, new PromptOptions { Steer = false }, cancellationToken).ConfigureAwait(false))
                {
                    if (evt is CompletedEvent)
                    {
                        // Not appended here: RecordCompletionAsync writes the turn's single completed event once the status is final.
                        break;
                    }

                    await AppendAsync(input.SessionId, input.CorrelationId, HarnessEventMapper.ToPayload(evt), cancellationToken).ConfigureAwait(false);
                    appended++;
                    heartbeat($"appended:{appended}");
                }
            }

            await _sessions.RecordCompletionAsync(input.SessionId, SessionStatus.Completed, CancellationToken.None).ConfigureAwait(false);
            _streamer.Notify(input.SessionId);
        }
        catch (OperationCanceledException)
        {
            await SettleAsync(input.SessionId, SessionStatus.Cancelled, "Prompt cancelled.").ConfigureAwait(false);
            throw;
        }
#pragma warning disable CA1031 // The activities boundary settles the session as failed, then rethrows so the runner records the failure.
        catch (Exception)
        {
            await SettleAsync(input.SessionId, SessionStatus.Failed, "Prompt execution failed.").ConfigureAwait(false);
            throw;
        }
#pragma warning restore CA1031

        var session = await _repository.GetAsync(input.SessionId, CancellationToken.None).ConfigureAwait(false);
        return session is null || session.Events.Count == 0 ? 0 : session.Events[^1].Seq;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> CollectArtifactsAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <inheritdoc />
    public Task TeardownSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return !_sandboxIds.TryRemove(input.IdempotencyKey, out var sandboxId)
            ? Task.CompletedTask
            : _sandbox.TeardownAsync(sandboxId, cancellationToken);
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
        _streamer.Notify(sessionId);
    }

    private async Task SettleAsync(string sessionId, SessionStatus final, string errorDetail)
    {
        try
        {
            if (final == SessionStatus.Failed)
            {
                await AppendAsync(
                    sessionId,
                    sessionId,
                    new ErrorPayload { Message = errorDetail, Retryable = false },
                    CancellationToken.None).ConfigureAwait(false);
            }

            await _sessions.RecordCompletionAsync(sessionId, final, CancellationToken.None).ConfigureAwait(false);
            _streamer.Notify(sessionId);
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
