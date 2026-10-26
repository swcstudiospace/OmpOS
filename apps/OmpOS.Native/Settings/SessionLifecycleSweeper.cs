using OmpOS.Contracts.Agents;
using OmpOS.Native.Agents;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Settings;

/// <summary>
/// Applies the Runtime settings to the sessions of a <see cref="SessionEngineSet"/>: a session that is not running and has had no activity
/// for the idle stop window sleeps (its engine is stopped and it can be reopened where it stopped), and a completed session that has had
/// no activity for the retention window is removed from the registry. A running session is never touched. A pass runs when the settings
/// change, when the sweeper starts and then on a timer, so a saved value takes effect for the sessions that are open now and for new ones.
/// </summary>
/// <remarks>The pass stops engines, so it runs on the context that owns the models: the timer hands it to the dispatch hook of the head.</remarks>
internal sealed class SessionLifecycleSweeper : IAsyncDisposable
{
    /// <summary>How often the sweeper looks at the sessions when the head does not say.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);

    private readonly SessionEngineSet _host;
    private readonly TimeProvider _clock;
    private readonly Action<Action>? _dispatch;
    private readonly System.Threading.Lock _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private RuntimeSettingsValues _current;
    private readonly SemaphoreSlim _passGate = new(1, 1);
    private ITimer? _timer;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="SessionLifecycleSweeper"/> class. It does nothing until <see cref="Start"/> or <see cref="Apply"/>.</summary>
    /// <param name="host">The engines and the registry whose sessions are swept.</param>
    /// <param name="initial">The windows in force.</param>
    /// <param name="clock">The clock the windows are measured on.</param>
    /// <param name="dispatch">Runs a pass on the context that owns the models, or null to run it inline.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/>, <paramref name="initial"/> or <paramref name="clock"/> is null.</exception>
    public SessionLifecycleSweeper(SessionEngineSet host, RuntimeSettingsValues initial, TimeProvider clock, Action<Action>? dispatch)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(clock);
        _host = host;
        _current = initial;
        _clock = clock;
        _dispatch = dispatch;
    }

    /// <summary>Gets the windows in force.</summary>
    public RuntimeSettingsValues Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>Starts the timer and makes a first pass. A non-positive or infinite interval starts no timer.</summary>
    /// <param name="interval">How often a pass runs.</param>
    public void Start(TimeSpan interval)
    {
        if (interval > TimeSpan.Zero && interval != Timeout.InfiniteTimeSpan)
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _disposed) != 0 || _timer is not null)
                {
                    return;
                }

                _timer = _clock.CreateTimer(static state => ((SessionLifecycleSweeper)state!).Request(), this, interval, interval);
            }
        }

        Request();
    }

    /// <summary>Puts new windows in force and makes a pass with them when they differ from the ones in force.</summary>
    /// <param name="values">The windows.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    public void Apply(RuntimeSettingsValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        lock (_gate)
        {
            if (_current == values)
            {
                return;
            }

            _current = values;
        }

        Request();
    }

    /// <summary>Makes one pass now over the sessions of the registry. A test calls it directly.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>What the pass did.</returns>
    public async Task<SweepOutcome> SweepAsync(CancellationToken cancellationToken)
    {
        var registry = _host.Registry;
        if (registry is null)
        {
            return SweepOutcome.None;
        }

        var windows = Current;
        var now = _clock.GetUtcNow();
        var slept = 0;
        var removed = 0;
        foreach (var record in registry.SnapshotRecords())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quiet = now - record.LastActivity;
            if (record.Lifecycle == SessionLifecycle.Completed && quiet >= windows.Retention)
            {
                await StopEngineAsync(record.Id, cancellationToken).ConfigureAwait(true);
                if (registry.Close(record.Id))
                {
                    removed++;

                    // The engine is stopped and the record is gone, so nothing writes under the session's own directory any more.
                    _ = registry.DeleteStateDirectory(record.Id);
                }

                continue;
            }

            if (record.Lifecycle != SessionLifecycle.Running && quiet >= windows.IdleStop && !_host.IsClosed(record.Id))
            {
                await StopEngineAsync(record.Id, cancellationToken).ConfigureAwait(true);
                slept++;
            }
        }

        return new SweepOutcome(slept, removed);
    }

    /// <summary>Stops the timer and waits for the pass that is running.</summary>
    /// <returns>A task that completes when the sweeper is stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        ITimer? timer;
        lock (_gate)
        {
            timer = _timer;
            _timer = null;
        }

        if (timer is not null)
        {
            await timer.DisposeAsync().ConfigureAwait(true);
        }

        await _lifetime.CancelAsync().ConfigureAwait(true);

        // Taking the gate waits for the pass that is running, and keeping it stops every later pass.
        await _passGate.WaitAsync(CancellationToken.None).ConfigureAwait(true);
        _lifetime.Dispose();
        _passGate.Dispose();
    }

    private async Task StopEngineAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!_host.IsClosed(sessionId))
        {
            await _host.CloseSessionAsync(sessionId, cancellationToken).ConfigureAwait(true);
        }
    }

    private void Request()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        void Begin() => _ = PassAsync();

        if (_dispatch is null)
        {
            Begin();
        }
        else
        {
            _dispatch(Begin);
        }
    }

    private async Task PassAsync()
    {
        // A pass that is already running covers this request: the next one starts from the registry as it is then.
        bool entered;
        try
        {
            entered = await _passGate.WaitAsync(TimeSpan.Zero, CancellationToken.None).ConfigureAwait(true);
        }
        catch (ObjectDisposedException)
        {
            // The sweeper was disposed between the request and the pass.
            return;
        }

        if (!entered)
        {
            return;
        }

        try
        {
            await SweepAsync(_lifetime.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The head is closing.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A failed pass leaves the sessions as they were; the next pass tries again.
        }
        finally
        {
            _passGate.Release();
        }
    }
}
