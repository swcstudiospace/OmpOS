using Autonogrammer.Sessions;

namespace Autonogrammer.Api.Hosting;

/// <summary>
/// Opens <see cref="HostingStartupGate"/> after the session store answered one read. A failed read is retried every
/// <see cref="RetryInterval"/> until the host stops.
/// </summary>
internal sealed partial class StartupGateHostedService : BackgroundService
{
    /// <summary>Gets the pause between failed probes.</summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private readonly ISessionRepository _repository;
    private readonly HostingStartupGate _gate;
    private readonly TimeProvider _time;
    private readonly ILogger<StartupGateHostedService> _logger;

    /// <summary>Initializes a new instance of the <see cref="StartupGateHostedService"/> class.</summary>
    /// <param name="repository">The session store to probe.</param>
    /// <param name="gate">The gate to open.</param>
    /// <param name="timeProvider">The clock used for the retry pause.</param>
    /// <param name="logger">The logger.</param>
    public StartupGateHostedService(ISessionRepository repository, HostingStartupGate gate, TimeProvider timeProvider, ILogger<StartupGateHostedService> logger)
    {
        _repository = repository;
        _gate = gate;
        _time = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _ = await _repository.GetAsync("__health__", stoppingToken).ConfigureAwait(false);
                _gate.Open();
                LogGateOpened(_logger);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Only the exception type is logged: a store error message can quote connection details.
                LogProbeFailed(_logger, ex.GetType().Name);
            }

            try
            {
                await Task.Delay(RetryInterval, _time, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Startup gate opened: the session store is reachable.")]
    private static partial void LogGateOpened(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Startup probe of the session store failed ({ExceptionType}); retrying.")]
    private static partial void LogProbeFailed(ILogger logger, string exceptionType);
}
