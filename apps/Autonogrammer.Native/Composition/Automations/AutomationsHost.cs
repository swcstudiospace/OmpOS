using Microsoft.Extensions.Logging;
using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Presentation.Automations;
using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Workspace;

namespace Autonogrammer.Native.Composition.Automations;

/// <summary>
/// The automations of this computer: their file store, the runner with its local and cloud targets, the scheduler that fires the cron triggers while the application runs, and the manager the Settings page
/// draws. The head creates it with its other services, hands it the sessions once the agent panel exists, starts it after the head is open and disposes it on shutdown, which cancels the runs in flight.
/// Secrets are only ever references: a local automation has none, and a cloud automation holds secret names the cloud service resolves.
/// </summary>
internal sealed class AutomationsHost : IAsyncDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private static readonly Action<ILogger, string, Exception?> SchedulerFaulted =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(60, "AutomationSchedulerFaulted"), "The automation scheduler could not read its files ({ExceptionType}); it tries again in a minute.");

    private readonly FileAutomationStore _store;
    private readonly FileSetupConsentStore _consent;
    private readonly AutomationRunner _runner;
    private readonly TimeProvider _clock;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly System.Threading.Lock _gate = new();
    private IAutomationSessions? _sessions;
    private Task _loop = Task.CompletedTask;
    private int _started;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="AutomationsHost"/> class. It opens no file and starts nothing.</summary>
    /// <param name="storePath">The file that holds the automations, their cursors and their history.</param>
    /// <param name="consentPath">The file that holds the automations the user allowed to run.</param>
    /// <param name="cloud">The cloud agents host, or null when the head has none.</param>
    /// <param name="workspaceRoot">Returns the workspace root the local runs work in.</param>
    /// <param name="localAvailable">Whether a local automation can run: omp was found.</param>
    /// <param name="clock">The clock the scheduler, the runner and the run timeouts use.</param>
    /// <param name="logger">The logger, or null to discard.</param>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="cloud"/> or <paramref name="logger"/> is null.</exception>
    public AutomationsHost(string storePath, string consentPath, ICloudAgentsHost? cloud, Func<string> workspaceRoot, bool localAvailable, TimeProvider clock, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(consentPath);
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
        _logger = logger;
        LocalAvailable = localAvailable;
        CloudAvailable = cloud is not null;
        _store = new FileAutomationStore(storePath);
        _consent = new FileSetupConsentStore(consentPath, clock);
        var targets = new List<IAutomationTarget>();
        if (localAvailable)
        {
            targets.Add(new LocalSessionAutomationTarget(() => Volatile.Read(ref _sessions), workspaceRoot, _consent));
        }

        if (cloud is not null)
        {
            targets.Add(new CloudHostAutomationTarget(cloud));
        }

        _runner = new AutomationRunner(_store, targets, clock);
        Scheduler = new AutomationScheduler(_store, _runner, clock);
        Manager = new LocalAutomationsManager(new AutomationService(_store, new InMemoryAutomationSecretStore(), clock), _store, _runner, clock, localAvailable, CloudAvailable);
    }

    /// <summary>Gets the manager the Settings page draws.</summary>
    public IAutomationsManager Manager { get; }

    /// <summary>Gets the scheduler. The host runs it after <see cref="Start"/>; a test can tick it by hand.</summary>
    public AutomationScheduler Scheduler { get; }

    /// <summary>Gets the store of the automations and their history.</summary>
    public IAutomationStore Store => _store;

    /// <summary>Gets the runner, which admits a run under the concurrency and hourly limits of its automation.</summary>
    public AutomationRunner Runner => _runner;

    /// <summary>Gets whether a local automation can run on this computer.</summary>
    public bool LocalAvailable { get; }

    /// <summary>Gets whether the head has a cloud agents service a cloud automation can run on.</summary>
    public bool CloudAvailable { get; }

    /// <summary>Gets whether <see cref="Start"/> was called.</summary>
    public bool IsStarted => Volatile.Read(ref _started) != 0;

    /// <summary>Gives the host the sessions its local runs happen in. The head calls it once the agent panel exists; a run that fires earlier fails with a message and is recorded.</summary>
    /// <param name="sessions">The sessions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sessions"/> is null.</exception>
    public void Attach(IAutomationSessions sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        Volatile.Write(ref _sessions, sessions);
    }

    /// <summary>
    /// Starts the scheduler: runs left running by an earlier process are marked failed, then the cron triggers are checked every <see cref="AutomationScheduler.TickInterval"/> until <see cref="DisposeAsync"/>.
    /// Calling it again does nothing. It never throws; a failure to read the files is logged and retried.
    /// </summary>
    public void Start()
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        lock (_gate)
        {
            _loop = Task.Run(() => LoopAsync(_stop.Token), CancellationToken.None);
        }
    }

    /// <summary>Stops the scheduler, cancels the runs in flight, waits for them to record their end and releases the files.</summary>
    /// <returns>A task that completes when the runs ended.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _stop.CancelAsync().ConfigureAwait(false);
        Task loop;
        lock (_gate)
        {
            loop = _loop;
        }

        await loop.ConfigureAwait(false);
        await _runner.DisposeAsync().ConfigureAwait(false);
        _store.Dispose();
        _consent.Dispose();
        _stop.Dispose();
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _ = await _runner.RecoverInterruptedRunsAsync(cancellationToken).ConfigureAwait(false);
                    await Scheduler.RunAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                {
                    if (_logger is not null)
                    {
                        SchedulerFaulted(_logger, exception.GetType().Name, null);
                    }

                    await Task.Delay(RetryDelay, _clock, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The application is closing.
        }
    }
}
