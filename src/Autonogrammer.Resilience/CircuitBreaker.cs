using Microsoft.Extensions.Logging;

namespace Autonogrammer.Resilience;

/// <summary>
/// A consecutive-and-ratio circuit breaker. It opens after <see cref="ResiliencePolicy.BreakerConsecutiveFailures"/> failures in a row, or
/// when at least <see cref="ResiliencePolicy.BreakerFailureRatio"/> of the last <see cref="ResiliencePolicy.BreakerSampledCalls"/> calls
/// failed and all of them fall inside <see cref="ResiliencePolicy.BreakerWindow"/>. After the open duration it admits exactly one probe; the
/// probe's outcome closes the breaker or opens it for another full duration.
/// </summary>
internal sealed class CircuitBreaker
{
    private static readonly Action<ILogger, string, int, Exception?> Opened =
        LoggerMessage.Define<string, int>(
            LogLevel.Warning,
            new EventId(6801, "ResilienceBreakerOpened"),
            "The {Component} circuit breaker opened after {Failures} recent failures; calls fail fast until a probe succeeds.");

    private static readonly Action<ILogger, string, Exception?> HalfOpened =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(6802, "ResilienceBreakerHalfOpened"),
            "The {Component} circuit breaker admits one probe call.");

    private static readonly Action<ILogger, string, Exception?> Closed =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(6803, "ResilienceBreakerClosed"),
            "The {Component} circuit breaker closed after a successful probe.");

    private readonly ResiliencePolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger? _logger;
    private readonly System.Threading.Lock _gate = new();
    private readonly Queue<(DateTimeOffset At, bool Failed)> _window = new();

    private BreakerState _state = BreakerState.Closed;
    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;
    private bool _probeInFlight;

    /// <summary>Initializes a new instance of the <see cref="CircuitBreaker"/> class.</summary>
    /// <param name="policy">The component policy.</param>
    /// <param name="timeProvider">The clock that measures the open duration and the ratio window.</param>
    /// <param name="logger">The logger, or <see langword="null"/>.</param>
    public CircuitBreaker(ResiliencePolicy policy, TimeProvider timeProvider, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _policy = policy;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private enum BreakerState
    {
        Closed,
        Open,
        HalfOpen,
    }

    /// <summary>Gets what the breaker says about the dependency.</summary>
    public DependencyState State
    {
        get
        {
            lock (_gate)
            {
                if (_state == BreakerState.Open)
                {
                    return DependencyState.Open;
                }

                if (_state == BreakerState.HalfOpen)
                {
                    return DependencyState.Degraded;
                }

                Trim(_timeProvider.GetUtcNow());
                return _consecutiveFailures == 0 && !_window.Any(sample => sample.Failed) ? DependencyState.Healthy : DependencyState.Degraded;
            }
        }
    }

    /// <summary>Asks to make a call.</summary>
    /// <returns>The permit; pass it back to <see cref="RecordSuccess"/>, <see cref="RecordFailure"/> or <see cref="Release"/> when the call ended.</returns>
    public BreakerPermit TryAcquire()
    {
        var transitioned = false;
        BreakerPermit permit;
        lock (_gate)
        {
            switch (_state)
            {
                case BreakerState.Closed:
                    return BreakerPermit.Allowed;
                case BreakerState.Open when _timeProvider.GetUtcNow() - _openedAt >= _policy.BreakerOpenDuration:
                    _state = BreakerState.HalfOpen;
                    _probeInFlight = true;
                    transitioned = true;
                    permit = BreakerPermit.Probe;
                    break;
                case BreakerState.HalfOpen when !_probeInFlight:
                    _probeInFlight = true;
                    permit = BreakerPermit.Probe;
                    break;
                default:
                    return BreakerPermit.Rejected;
            }
        }

        if (transitioned)
        {
            ResilienceTelemetry.RecordBreakerTransition(_policy.Component, "half_open", "open_elapsed");
            if (_logger is not null)
            {
                HalfOpened(_logger, _policy.Component, null);
            }
        }

        return permit;
    }

    /// <summary>Records that a permitted call succeeded, or that the dependency answered even though the call failed for another reason.</summary>
    /// <param name="permit">The permit the call was admitted with.</param>
    public void RecordSuccess(BreakerPermit permit)
    {
        var closed = false;
        lock (_gate)
        {
            if (permit == BreakerPermit.Probe && _state == BreakerState.HalfOpen)
            {
                _state = BreakerState.Closed;
                _probeInFlight = false;
                _consecutiveFailures = 0;
                _window.Clear();
                closed = true;
            }
            else if (permit == BreakerPermit.Allowed && _state == BreakerState.Closed)
            {
                _consecutiveFailures = 0;
                Add(_timeProvider.GetUtcNow(), failed: false);
            }
        }

        if (closed)
        {
            ResilienceTelemetry.RecordBreakerTransition(_policy.Component, "closed", "probe_succeeded");
            if (_logger is not null)
            {
                Closed(_logger, _policy.Component, null);
            }
        }
    }

    /// <summary>Records that a permitted call failed because the dependency is failing.</summary>
    /// <param name="permit">The permit the call was admitted with.</param>
    public void RecordFailure(BreakerPermit permit)
    {
        string? reason = null;
        var failures = 0;
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            if (permit == BreakerPermit.Probe && _state == BreakerState.HalfOpen)
            {
                Open(now);
                failures = _consecutiveFailures;
                reason = "probe_failed";
            }
            else if (permit == BreakerPermit.Allowed && _state == BreakerState.Closed)
            {
                _consecutiveFailures++;
                Add(now, failed: true);
                if (_consecutiveFailures >= _policy.BreakerConsecutiveFailures)
                {
                    reason = "consecutive_failures";
                }
                else if (RatioExceeded())
                {
                    reason = "failure_ratio";
                }

                if (reason is not null)
                {
                    failures = _window.Count(sample => sample.Failed);
                    failures = Math.Max(failures, _consecutiveFailures);
                    Open(now);
                }
            }
        }

        if (reason is not null)
        {
            ResilienceTelemetry.RecordBreakerTransition(_policy.Component, "open", reason);
            if (_logger is not null)
            {
                Opened(_logger, _policy.Component, failures, null);
            }
        }
    }

    /// <summary>Gives a permit back without a verdict, for a call that ended for a reason that says nothing about the dependency.</summary>
    /// <param name="permit">The permit the call was admitted with.</param>
    public void Release(BreakerPermit permit)
    {
        if (permit != BreakerPermit.Probe)
        {
            return;
        }

        lock (_gate)
        {
            if (_state == BreakerState.HalfOpen)
            {
                _probeInFlight = false;
            }
        }
    }

    private void Open(DateTimeOffset now)
    {
        _state = BreakerState.Open;
        _openedAt = now;
        _probeInFlight = false;
    }

    private void Add(DateTimeOffset now, bool failed)
    {
        _window.Enqueue((now, failed));
        Trim(now);
    }

    private void Trim(DateTimeOffset now)
    {
        while (_window.Count > _policy.BreakerSampledCalls || (_window.Count > 0 && now - _window.Peek().At > _policy.BreakerWindow))
        {
            _ = _window.Dequeue();
        }
    }

    private bool RatioExceeded()
    {
        if (_window.Count < _policy.BreakerSampledCalls)
        {
            return false;
        }

        var failed = _window.Count(sample => sample.Failed);
        return failed >= _window.Count * _policy.BreakerFailureRatio;
    }
}
