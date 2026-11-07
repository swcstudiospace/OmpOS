using Autonogrammer.Resilience;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// The resilience pipeline of every Dragonfly store call, plus the two failure modes of D-11. A closed call (lease, idempotency, nonce) ends in a
/// <see cref="DataPlaneTimeoutException"/> when Dragonfly cannot answer, so the API refuses the request with 503. An open call (signals, presence)
/// reports the outage as <see langword="false"/> and the caller carries on. One instance serves all stores over one connection, so they share one breaker.
/// </summary>
public sealed class DragonflyResilience
{
    private DragonflyResilience(DragonflyResilienceOptions options, ResiliencePipeline pipeline)
    {
        Options = options;
        Pipeline = pipeline;
    }

    /// <summary>Gets the options the pipeline was built from.</summary>
    public DragonflyResilienceOptions Options { get; }

    /// <summary>Gets the pipeline.</summary>
    public ResiliencePipeline Pipeline { get; }

    /// <summary>Gets what the breaker says about Dragonfly right now.</summary>
    public DependencyState State => Pipeline.State;

    /// <summary>Builds the pipeline with the Dragonfly failure classification.</summary>
    /// <param name="options">The options, or <see langword="null"/> for the defaults of D-41.</param>
    /// <param name="timeProvider">The clock, or <see langword="null"/> for the system clock.</param>
    /// <param name="logger">The logger, or <see langword="null"/>.</param>
    /// <returns>The resilience of one Dragonfly connection.</returns>
    public static DragonflyResilience Create(DragonflyResilienceOptions? options = null, TimeProvider? timeProvider = null, ILogger<ResiliencePipeline>? logger = null)
    {
        var effective = options ?? new DragonflyResilienceOptions();
        var pipeline = new ResiliencePipeline(effective.ToPolicy(IsOutage), timeProvider ?? TimeProvider.System, logger: logger);
        return new DragonflyResilience(effective, pipeline);
    }

    /// <summary>Runs a call whose failure must refuse the request.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="idempotent">Whether the call is safe to run twice. A <c>SET NX</c> claim is not: a retry after a lost reply would misreport it.</param>
    /// <param name="operation">The call. It is ended at the attempt timeout even though the client ignores the token.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The result of the call.</returns>
    /// <exception cref="DataPlaneTimeoutException">Dragonfly could not answer in time, refused the connection, or the breaker or bulkhead refused the call.</exception>
    internal async Task<T> ClosedAsync<T>(bool idempotent, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        try
        {
            return await Pipeline.ExecuteAsync(OperationClass.Interactive, idempotent, ct => operation().WaitAsync(ct), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsOutage(exception))
        {
            throw new DataPlaneTimeoutException(Pipeline.Component, DataPlaneTimeoutReason.Timeout);
        }
    }

    /// <summary>Runs a call that returns nothing and whose failure must refuse the request.</summary>
    /// <param name="idempotent">Whether the call is safe to run twice.</param>
    /// <param name="operation">The call.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>A task that completes when the call has.</returns>
    /// <exception cref="DataPlaneTimeoutException">Dragonfly could not answer.</exception>
    internal Task ClosedAsync(bool idempotent, Func<Task> operation, CancellationToken cancellationToken)
    {
        return ClosedAsync(
            idempotent,
            async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }

    /// <summary>Runs a call that is only a hint: an outage is reported, never thrown.</summary>
    /// <param name="operation">The idempotent call.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns><see langword="true"/> when the call ran; <see langword="false"/> when Dragonfly could not answer.</returns>
    internal async Task<bool> TryOpenAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        try
        {
            await ClosedAsync(true, operation, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DataPlaneTimeoutException)
        {
            return false;
        }
    }

    /// <summary>Tells an outage (the breaker counts it, an idempotent call retries it) from a server answer.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns><see langword="true"/> when Dragonfly could not be reached or did not answer in time.</returns>
    internal static bool IsOutage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is RedisConnectionException or RedisTimeoutException || ResiliencePolicy.DefaultIsTransient(exception);
    }
}
