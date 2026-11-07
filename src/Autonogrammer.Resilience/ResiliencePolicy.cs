using System.Net.Sockets;

namespace Autonogrammer.Resilience;

/// <summary>
/// Everything one <see cref="ResiliencePipeline"/> needs to know about one component. Build it from the option classes
/// (<see cref="PostgresResilienceOptions.ToPolicy"/> and its siblings) so the defaults of D-41 apply.
/// </summary>
public sealed record ResiliencePolicy
{
    /// <summary>Gets the component name used in exceptions, logs and metric tags, for example <c>postgres</c>. Never a connection string.</summary>
    public required string Component { get; init; }

    /// <summary>Gets how long one attempt of an <see cref="OperationClass.Interactive"/> call may run.</summary>
    public required TimeSpan InteractiveTimeout { get; init; }

    /// <summary>Gets how long one attempt of a <see cref="OperationClass.Background"/> call may run.</summary>
    public required TimeSpan BackgroundTimeout { get; init; }

    /// <summary>Gets how many times a failed call may be retried after its first attempt. Zero disables retries.</summary>
    public int MaxRetries { get; init; }

    /// <summary>Gets the lower bound of the decorrelated-jitter retry delay.</summary>
    public TimeSpan MinDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Gets the upper bound of the decorrelated-jitter retry delay.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets the total time a call and its retries may take. <see langword="null"/> derives it from the other values: every attempt at its
    /// timeout plus every retry at <see cref="MaxDelay"/>. A call is never shorter than the timeout of its own class.
    /// </summary>
    public TimeSpan? Budget { get; init; }

    /// <summary>Gets how many <see cref="OperationClass.Interactive"/> calls may be in flight.</summary>
    public required int InteractiveInFlight { get; init; }

    /// <summary>Gets how many <see cref="OperationClass.Background"/> calls may be in flight, or <see langword="null"/> to share the interactive limit.</summary>
    public int? BackgroundInFlight { get; init; }

    /// <summary>Gets the number of consecutive failures that opens the breaker.</summary>
    public int BreakerConsecutiveFailures { get; init; } = ResilienceOptions.BreakerConsecutiveFailures;

    /// <summary>Gets the failure share of the sampled calls that opens the breaker.</summary>
    public double BreakerFailureRatio { get; init; } = ResilienceOptions.BreakerFailureRatio;

    /// <summary>Gets how many recent calls the ratio rule samples.</summary>
    public int BreakerSampledCalls { get; init; } = ResilienceOptions.BreakerSampledCalls;

    /// <summary>Gets how long a call counts towards the ratio rule.</summary>
    public TimeSpan BreakerWindow { get; init; } = TimeSpan.FromSeconds(ResilienceOptions.BreakerWindowSeconds);

    /// <summary>Gets how long the breaker stays open before it admits one probe.</summary>
    public required TimeSpan BreakerOpenDuration { get; init; }

    /// <summary>
    /// Gets the predicate that says whether a failure is a transient dependency failure: one the breaker counts and a retry may cure.
    /// The default knows only BCL types (timeouts, I/O, sockets, HTTP); a store passes its driver's own predicate, for example
    /// <c>NpgsqlException.IsTransient</c>.
    /// </summary>
    public Func<Exception, bool> IsTransient { get; init; } = DefaultIsTransient;

    /// <summary>
    /// Gets the predicate that says whether a failure is a serialisation failure, which <see cref="ResiliencePipeline.ExecuteTransactionAsync{T}"/>
    /// answers with one retry of the whole transaction. The default never matches; the PostgreSQL stores pass a SQLSTATE 40001 check.
    /// </summary>
    public Func<Exception, bool> IsSerializationFailure { get; init; } = static _ => false;

    /// <summary>The default transient classification: timeouts, I/O, socket and HTTP failures.</summary>
    /// <param name="exception">The failure to classify.</param>
    /// <returns><see langword="true"/> when the failure is transient.</returns>
    public static bool DefaultIsTransient(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is TimeoutException or IOException or SocketException or HttpRequestException;
    }

    /// <summary>Gets the timeout of one attempt of the given class.</summary>
    /// <param name="operationClass">The class of the call.</param>
    /// <returns>The attempt timeout.</returns>
    internal TimeSpan TimeoutOf(OperationClass operationClass) =>
        operationClass == OperationClass.Background ? BackgroundTimeout : InteractiveTimeout;

    /// <summary>Gets the total time a call of the given class may take, retries and delays included.</summary>
    /// <param name="operationClass">The class of the call.</param>
    /// <returns>The total deadline of the call.</returns>
    internal TimeSpan TotalOf(OperationClass operationClass)
    {
        var timeout = TimeoutOf(operationClass);
        var derived = (timeout * (MaxRetries + 1)) + (MaxDelay * MaxRetries);
        var budget = Budget ?? derived;
        return budget > timeout ? budget : timeout;
    }

    /// <summary>Checks the values the pipeline relies on.</summary>
    /// <exception cref="ArgumentException">A value is out of range; the message names the property.</exception>
    internal void Validate()
    {
        Require(!string.IsNullOrWhiteSpace(Component), nameof(Component));
        Require(InteractiveTimeout > TimeSpan.Zero, nameof(InteractiveTimeout));
        Require(BackgroundTimeout > TimeSpan.Zero, nameof(BackgroundTimeout));
        Require(MaxRetries >= 0, nameof(MaxRetries));
        Require(MinDelay > TimeSpan.Zero && MaxDelay >= MinDelay, $"{nameof(MinDelay)}/{nameof(MaxDelay)}");
        Require(Budget is null || Budget > TimeSpan.Zero, nameof(Budget));
        Require(InteractiveInFlight > 0, nameof(InteractiveInFlight));
        Require(BackgroundInFlight is null or > 0, nameof(BackgroundInFlight));
        Require(BreakerConsecutiveFailures > 0, nameof(BreakerConsecutiveFailures));
        Require(BreakerFailureRatio is > 0 and <= 1, nameof(BreakerFailureRatio));
        Require(BreakerSampledCalls > 0, nameof(BreakerSampledCalls));
        Require(BreakerWindow > TimeSpan.Zero, nameof(BreakerWindow));
        Require(BreakerOpenDuration > TimeSpan.Zero, nameof(BreakerOpenDuration));
        Require(IsTransient is not null, nameof(IsTransient));
        Require(IsSerializationFailure is not null, nameof(IsSerializationFailure));
    }

    private static void Require(bool condition, string property)
    {
        if (!condition)
        {
            throw new ArgumentException($"The resilience policy value {property} is out of range.", nameof(property));
        }
    }
}
