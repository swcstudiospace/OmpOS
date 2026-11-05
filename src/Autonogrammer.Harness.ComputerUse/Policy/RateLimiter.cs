namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>
/// Token bucket plus step counter plus consecutive-failure counter.
/// Defaults are 60 actuating actions per minute, burst 10, 300 steps, continue grant 100, and 5 failures.
/// </summary>
public sealed class RateLimiter
{
    private readonly TimeProvider _timeProvider;
    private readonly int _perMinute;
    private readonly int _burst;
    private readonly int _stepLimit;
    private readonly int _continueGrant;
    private readonly int _failurePause;
    private readonly Lock _gate = new();
    private readonly Queue<DateTimeOffset> _window = new();
    private double _tokens;
    private DateTimeOffset _refilledAt;
    private int _steps;
    private int _stepCap;
    private int _consecutiveFailures;
    private bool _paused;

    /// <summary>Creates a limiter from options.</summary>
    /// <param name="time">Clock.</param>
    /// <param name="perMinute">Actuating actions per minute. Default 60.</param>
    /// <param name="burst">Burst. Default 10.</param>
    /// <param name="stepLimit">Steps before a continue grant. Default 300.</param>
    /// <param name="continueGrant">Steps added by a continue grant. Default 100.</param>
    /// <param name="failurePause">Consecutive failures that pause. Default 5.</param>
    public RateLimiter(TimeProvider time, int perMinute = 60, int burst = 10, int stepLimit = 300, int continueGrant = 100, int failurePause = 5)
    {
        ArgumentNullException.ThrowIfNull(time);
        _timeProvider = time;
        _perMinute = perMinute;
        _burst = burst;
        _stepLimit = stepLimit;
        _continueGrant = continueGrant;
        _failurePause = failurePause;
        _tokens = burst;
        _stepCap = stepLimit;
        _refilledAt = time.GetUtcNow();
    }

    /// <summary>Configured actions per minute.</summary>
    public int PerMinute => _perMinute;

    /// <summary>Configured burst.</summary>
    public int Burst => _burst;

    /// <summary>Configured step limit.</summary>
    public int StepLimit => _stepLimit;

    /// <summary>Configured continue grant.</summary>
    public int ContinueGrant => _continueGrant;

    /// <summary>Configured failure pause.</summary>
    public int FailurePause => _failurePause;

    /// <summary><see langword="true"/> after <see cref="FailurePause"/> consecutive failures.</summary>
    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _paused;
            }
        }
    }

    /// <summary>Tries to take one actuating step.</summary>
    /// <returns>The refusal, or <see cref="RateLimitReason.None"/>.</returns>
    public RateLimitReason TryConsume()
    {
        lock (_gate)
        {
            if (_paused)
            {
                return RateLimitReason.FailurePause;
            }

            if (_steps >= _stepCap)
            {
                return RateLimitReason.StepLimit;
            }

            Refill();
            if (_tokens < 1 || _window.Count >= _perMinute)
            {
                return RateLimitReason.RateLimited;
            }

            _tokens -= 1;
            _window.Enqueue(_timeProvider.GetUtcNow());
            _steps++;
            return RateLimitReason.None;
        }
    }

    /// <summary>Moves the step counter to the cap. Tests use this to reach the step cap without burning the burst.</summary>
    public void ExhaustSteps()
    {
        lock (_gate)
        {
            _steps = _stepCap;
        }
    }

    /// <summary>Adds the continue grant to the step cap.</summary>
    public void GrantContinue()
    {
        lock (_gate)
        {
            _stepCap += _continueGrant;
        }
    }

    /// <summary>Records a failure. The fifth consecutive failure pauses the loop.</summary>
    public void NoteFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= _failurePause)
            {
                _paused = true;
            }
        }
    }

    /// <summary>Clears the consecutive-failure counter. Does not resume a pause.</summary>
    public void NoteSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
        }
    }

    /// <summary>Resumes after the user clears a failure pause.</summary>
    public void Resume()
    {
        lock (_gate)
        {
            _paused = false;
            _consecutiveFailures = 0;
        }
    }

    private void Refill()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        while (_window.Count > 0 && now - _window.Peek() >= TimeSpan.FromMinutes(1))
        {
            _window.Dequeue();
        }

        double elapsedSeconds = (now - _refilledAt).TotalSeconds;
        if (elapsedSeconds > 0)
        {
            _tokens = Math.Min(_burst, _tokens + (elapsedSeconds * _perMinute / 60d));
            _refilledAt = now;
        }
    }
}
