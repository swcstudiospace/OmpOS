using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Resilience;

/// <summary>
/// Runs every call to one data-plane component (PostgreSQL, Dragonfly, the audit ledger) under one bounded policy: a deadline per attempt and
/// a total deadline for the call, bounded decorrelated-jitter retries for idempotent calls only, a consecutive-and-ratio circuit breaker and
/// a bulkhead per operation class. All waiting is on the injected <see cref="TimeProvider"/>, so tests run on a manual clock.
/// </summary>
/// <remarks>
/// Cancellation is never swallowed: when the caller's token ends a call, the original <see cref="OperationCanceledException"/> (or whatever the
/// operation threw while the caller's token was cancelled) reaches the caller untouched, is not retried and is not a breaker failure. A deadline,
/// an open breaker or a full bulkhead surface as <see cref="DataPlaneTimeoutException"/>, which carries the component and a closed reason and
/// never an inner exception, so a provider message that quotes a connection string cannot leak through it.
/// </remarks>
public sealed class ResiliencePipeline
{
    private static readonly Action<ILogger, string, int, string, Exception?> RetryScheduled =
        LoggerMessage.Define<string, int, string>(
            LogLevel.Information,
            new EventId(6811, "ResilienceRetryScheduled"),
            "A {Component} call is retried (retry {Retry}) after a {Reason} failure.");

    private static readonly Action<ILogger, string, Exception?> CallTimedOut =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(6812, "ResilienceCallTimedOut"),
            "A {Component} call exceeded its deadline.");

    private static readonly Action<ILogger, string, string, Exception?> CallRejected =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(6813, "ResilienceCallRejected"),
            "A {Component} call was refused without reaching the dependency: {Reason}.");

    private readonly ResiliencePolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly RetryPolicy _retry;
    private readonly CircuitBreaker _breaker;
    private readonly Bulkhead _interactive;
    private readonly Bulkhead _background;
    private readonly ILogger<ResiliencePipeline>? _logger;

    /// <summary>Initializes a new instance of the <see cref="ResiliencePipeline"/> class.</summary>
    /// <param name="policy">The policy of the component, normally built by <c>PostgresResilienceOptions.ToPolicy</c> or a sibling.</param>
    /// <param name="timeProvider">The clock for deadlines, retry delays, the breaker and the call duration.</param>
    /// <param name="random">The source of retry jitter; defaults to <see cref="Random.Shared"/>.</param>
    /// <param name="logger">The logger, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">A policy value is out of range.</exception>
    public ResiliencePipeline(
        ResiliencePolicy policy,
        TimeProvider timeProvider,
        Random? random = null,
        ILogger<ResiliencePipeline>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(timeProvider);
        policy.Validate();
        _policy = policy;
        _timeProvider = timeProvider;
        _logger = logger;
        _retry = new RetryPolicy(policy, random);
        _breaker = new CircuitBreaker(policy, timeProvider, logger);
        _interactive = new Bulkhead(policy.InteractiveInFlight);
        _background = policy.BackgroundInFlight is { } background ? new Bulkhead(background) : _interactive;
    }

    private enum RetryMode
    {
        Never,
        Idempotent,
        WholeTransaction,
    }

    /// <summary>Gets the component this pipeline protects.</summary>
    public string Component => _policy.Component;

    /// <summary>Gets what the breaker says about the dependency right now.</summary>
    public DependencyState State => _breaker.State;

    /// <summary>Runs one call.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operationClass">Which timeout and bulkhead the call uses.</param>
    /// <param name="idempotent">
    /// Whether the call is safe to run twice: a read, an idempotent statement or script, or a write guarded by a unique key. Only such calls are
    /// retried; a write that is not guarded must pass <see langword="false"/>.
    /// </param>
    /// <param name="operation">The call. It must honour the token it is given: the token ends it at the attempt timeout.</param>
    /// <param name="cancellationToken">The caller's token. It passes through unchanged.</param>
    /// <returns>The result of the call.</returns>
    /// <exception cref="DataPlaneTimeoutException">The deadline expired, the breaker is open or the bulkhead is full.</exception>
    public Task<T> ExecuteAsync<T>(
        OperationClass operationClass,
        bool idempotent,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return RunAsync(operationClass, idempotent ? RetryMode.Idempotent : RetryMode.Never, operation, cancellationToken);
    }

    /// <summary>Runs one call that has no result.</summary>
    /// <param name="operationClass">Which timeout and bulkhead the call uses.</param>
    /// <param name="idempotent">Whether the call is safe to run twice; see <see cref="ExecuteAsync{T}"/>.</param>
    /// <param name="operation">The call. It must honour the token it is given.</param>
    /// <param name="cancellationToken">The caller's token. It passes through unchanged.</param>
    /// <returns>A task that completes when the call has.</returns>
    /// <exception cref="DataPlaneTimeoutException">The deadline expired, the breaker is open or the bulkhead is full.</exception>
    public Task ExecuteAsync(
        OperationClass operationClass,
        bool idempotent,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ExecuteAsync(
            operationClass,
            idempotent,
            async ct =>
            {
                await operation(ct).ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Runs one whole transaction: when it fails with a serialisation failure (<see cref="ResiliencePolicy.IsSerializationFailure"/>) the
    /// operation runs again from the start, once. Every other failure is not retried, so the operation must open, run and commit the
    /// transaction itself on each call.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operationClass">Which timeout and bulkhead the call uses.</param>
    /// <param name="operation">The whole transaction. It must honour the token it is given.</param>
    /// <param name="cancellationToken">The caller's token. It passes through unchanged.</param>
    /// <returns>The result of the transaction.</returns>
    /// <exception cref="DataPlaneTimeoutException">The deadline expired, the breaker is open or the bulkhead is full.</exception>
    public Task<T> ExecuteTransactionAsync<T>(
        OperationClass operationClass,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return RunAsync(operationClass, RetryMode.WholeTransaction, operation, cancellationToken);
    }

    private async Task<T> RunAsync<T>(
        OperationClass operationClass,
        RetryMode mode,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var call = new Call(operationClass, mode, _policy.MinDelay, _policy.TotalOf(operationClass), _timeProvider, cancellationToken);
        while (true)
        {
            var attempt = await AttemptAsync(call, operation).ConfigureAwait(false);
            if (attempt.Failure is null)
            {
                _breaker.RecordSuccess(attempt.Permit);
                RecordCall(call, "success", "none");
                return attempt.Result!;
            }

            var transient = !attempt.TimedOut && _policy.IsTransient(attempt.Failure);
            if (attempt.TimedOut || transient)
            {
                _breaker.RecordFailure(attempt.Permit);
            }
            else
            {
                // The dependency answered (a constraint violation, a serialisation failure): it is healthy.
                _breaker.RecordSuccess(attempt.Permit);
            }

            var reason = NextRetryReason(call, attempt.Failure, attempt.TimedOut);
            if (reason is null)
            {
                ThrowTerminal(call, attempt.Failure, attempt.TimedOut, transient);
            }

            call.RetriesDone++;
            ResilienceTelemetry.RecordRetry(_policy.Component, call.OperationClass, reason);
            if (_logger is not null)
            {
                RetryScheduled(_logger, _policy.Component, call.RetriesDone, reason, null);
            }

            await WaitBeforeRetryAsync(call).ConfigureAwait(false);
        }
    }

    private async Task<Attempt<T>> AttemptAsync<T>(Call call, Func<CancellationToken, Task<T>> operation)
    {
        var permit = _breaker.TryAcquire();
        if (permit == BreakerPermit.Rejected)
        {
            throw Refuse(call, DataPlaneTimeoutReason.CircuitOpen, "circuit_open");
        }

        var bulkhead = call.OperationClass == OperationClass.Background ? _background : _interactive;
        if (!bulkhead.TryEnter())
        {
            _breaker.Release(permit);
            ResilienceTelemetry.RecordBulkheadRejected(_policy.Component, call.OperationClass);
            throw Refuse(call, DataPlaneTimeoutReason.BulkheadFull, "bulkhead_full");
        }

        try
        {
            return await RunAttemptAsync(call, permit, operation).ConfigureAwait(false);
        }
        finally
        {
            bulkhead.Exit();
        }
    }

    private async Task<Attempt<T>> RunAttemptAsync<T>(Call call, BreakerPermit permit, Func<CancellationToken, Task<T>> operation)
    {
        var remaining = call.Total - (_timeProvider.GetUtcNow() - call.Started);
        var timeout = _policy.TimeoutOf(call.OperationClass);
        var limit = remaining < timeout ? remaining : timeout;
        using var attempt = new CancellationTokenSource(limit > TimeSpan.Zero ? limit : TimeSpan.Zero, _timeProvider);
        using var attemptScope = CancellationTokenSource.CreateLinkedTokenSource(call.Scope.Token, attempt.Token);
        try
        {
            var result = await operation(attemptScope.Token).ConfigureAwait(false);
            return new Attempt<T>(result, null, false, permit);
        }
        catch (Exception exception)
        {
            if (call.Caller.IsCancellationRequested)
            {
                // The caller asked for this: never swallow it, never retry it, never count it against the dependency.
                _breaker.Release(permit);
                RecordCall(call, "cancelled", "caller");
                throw;
            }

            return new Attempt<T>(default, exception, call.Deadline.IsCancellationRequested || attempt.IsCancellationRequested, permit);
        }
    }

    /// <summary>Decides whether the call runs again and, when it does, picks the delay and checks that the total budget covers it.</summary>
    /// <returns>The closed retry reason, or <see langword="null"/> when the call ends.</returns>
    private string? NextRetryReason(Call call, Exception failure, bool timedOut)
    {
        string reason;
        bool retry;
        if (timedOut)
        {
            reason = "timeout";
            retry = !call.Deadline.IsCancellationRequested && call.Mode == RetryMode.Idempotent && _retry.ShouldRetryAfterTimeout(call.RetriesDone, idempotent: true);
        }
        else if (call.Mode == RetryMode.WholeTransaction)
        {
            reason = "serialization";
            retry = _retry.ShouldRetryTransaction(failure, call.RetriesDone);
        }
        else
        {
            reason = "transient";
            retry = _retry.ShouldRetry(failure, call.RetriesDone, call.Mode == RetryMode.Idempotent);
        }

        if (!retry)
        {
            return null;
        }

        call.Delay = _retry.NextDelay(call.Delay);
        return _timeProvider.GetUtcNow() - call.Started + call.Delay < call.Total ? reason : null;
    }

    [DoesNotReturn]
    private void ThrowTerminal(Call call, Exception failure, bool timedOut, bool transient)
    {
        if (timedOut)
        {
            throw TimeoutFailure(call);
        }

        RecordCall(call, "failure", transient ? "transient" : "non_transient");
        ExceptionDispatchInfo.Throw(failure);
    }

    private async Task WaitBeforeRetryAsync(Call call)
    {
        try
        {
            await Task.Delay(call.Delay, _timeProvider, call.Scope.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!call.Caller.IsCancellationRequested && call.Deadline.IsCancellationRequested)
        {
            throw TimeoutFailure(call);
        }
        catch (OperationCanceledException)
        {
            RecordCall(call, "cancelled", "caller");
            throw;
        }
    }

    private DataPlaneTimeoutException TimeoutFailure(Call call)
    {
        RecordCall(call, "timeout", "deadline");
        if (_logger is not null)
        {
            CallTimedOut(_logger, _policy.Component, null);
        }

        return new DataPlaneTimeoutException(_policy.Component, DataPlaneTimeoutReason.Timeout);
    }

    private DataPlaneTimeoutException Refuse(Call call, DataPlaneTimeoutReason reason, string reasonTag)
    {
        RecordCall(call, "rejected", reasonTag);
        if (_logger is not null)
        {
            CallRejected(_logger, _policy.Component, reasonTag, null);
        }

        return new DataPlaneTimeoutException(_policy.Component, reason);
    }

    private void RecordCall(Call call, string result, string reason) =>
        ResilienceTelemetry.RecordCall(_policy.Component, call.OperationClass, result, reason, (_timeProvider.GetUtcNow() - call.Started).TotalMilliseconds);

    private readonly record struct Attempt<T>(T? Result, Exception? Failure, bool TimedOut, BreakerPermit Permit);

    /// <summary>The state of one call across its attempts: its clocks, its two cancellation sources and its retry count.</summary>
    private sealed class Call : IDisposable
    {
        public Call(OperationClass operationClass, RetryMode mode, TimeSpan firstDelay, TimeSpan total, TimeProvider timeProvider, CancellationToken caller)
        {
            OperationClass = operationClass;
            Mode = mode;
            Delay = firstDelay;
            Total = total;
            Caller = caller;
            Started = timeProvider.GetUtcNow();

            // The deadline token runs on the injected clock; the caller token joins it, so either one ends the call.
            Deadline = new CancellationTokenSource(total, timeProvider);
            Scope = CancellationTokenSource.CreateLinkedTokenSource(caller, Deadline.Token);
        }

        public OperationClass OperationClass { get; }

        public RetryMode Mode { get; }

        public DateTimeOffset Started { get; }

        public TimeSpan Total { get; }

        public CancellationToken Caller { get; }

        public CancellationTokenSource Deadline { get; }

        public CancellationTokenSource Scope { get; }

        public int RetriesDone { get; set; }

        public TimeSpan Delay { get; set; }

        public void Dispose()
        {
            Scope.Dispose();
            Deadline.Dispose();
        }
    }
}
