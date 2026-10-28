using System.Collections.Concurrent;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Runs the slow steps of the control plane (creating a worker, waking one) in the background, tracked until they settle: no failure escapes
/// unlogged and no task is left unobserved. Host shutdown cancels what is running. <see cref="WhenIdleAsync"/> lets a test or a graceful shutdown wait for them.
/// </summary>
internal sealed class CloudBackgroundTasks
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<CloudBackgroundTasks> _logger;
    private readonly ConcurrentDictionary<long, Task> _running = new();
    private long _next;

    /// <summary>Initializes a new instance of the <see cref="CloudBackgroundTasks"/> class.</summary>
    /// <param name="lifetime">The host lifetime; its stop signal cancels running work.</param>
    /// <param name="logger">The logger.</param>
    public CloudBackgroundTasks(IHostApplicationLifetime lifetime, ILogger<CloudBackgroundTasks> logger)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(logger);
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>Starts a unit of work and tracks it.</summary>
    /// <param name="name">A short fixed name for the log, never user text.</param>
    /// <param name="work">The work. It receives the host's stop signal.</param>
    public void Start(string name, Func<CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(work);
        var id = Interlocked.Increment(ref _next);
        var task = Task.Run(() => RunAsync(name, work), CancellationToken.None);
        _running[id] = task;
        _ = task.ContinueWith(
            _ => _running.TryRemove(id, out Task? _),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Waits until every started unit of work has settled.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A task that completes when nothing is running.</returns>
    public async Task WhenIdleAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var snapshot = _running.Values.ToArray();
            if (snapshot.Length == 0)
            {
                return;
            }

            await Task.WhenAll(snapshot).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunAsync(string name, Func<CancellationToken, Task> work)
    {
        try
        {
            await work(_lifetime.ApplicationStopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            // The host is stopping: the work is abandoned and the sweeper repairs a half-finished agent.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Faulted(_logger, name, ex.GetType().FullName ?? "Unknown");
        }
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, string, Exception?> FaultedMessage =
            LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(70, "CloudBackgroundFaulted"), "Cloud background work {Name} faulted: {ErrorType}.");

        public static void Faulted(ILogger logger, string name, string errorType) => FaultedMessage(logger, name, errorType, null);
    }
}
