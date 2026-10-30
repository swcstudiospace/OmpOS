using Autonogrammer.Web.Demo;
using Autonogrammer.Web.Services;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>A Demo world on a manual clock with the web head's clients over it. Nothing touches the network.</summary>
internal sealed class DemoTestHost : IDisposable
{
    private readonly HttpClient _http;
    private readonly DemoApiHandler _demo;

    /// <summary>Initializes a new instance of the <see cref="DemoTestHost"/> class.</summary>
    /// <param name="autoStartRunning">Whether reading the "running" session starts its scripted turn.</param>
    internal DemoTestHost(bool autoStartRunning = false)
    {
        Time = new ManualTimeProvider();
        World = new DemoWorld(Time, new DemoOptions { AutoStartRunningSession = autoStartRunning });
        _demo = new DemoApiHandler(World);
        Interceptor = new InterceptHandler(_demo);
        Recorder = new RecordingHandler(Interceptor);
        _http = new HttpClient(Recorder) { BaseAddress = new Uri("http://demo.invalid/", UriKind.Absolute) };
        Api = new OmpApiClient(_http, new SseStreamOptions { InitialRetryDelay = TimeSpan.Zero, MaxRetryDelay = TimeSpan.Zero });
        Feed = new UserEventFeed(_http, new SseStreamOptions { InitialRetryDelay = TimeSpan.Zero, MaxRetryDelay = TimeSpan.Zero, MaxReconnects = null }, Time);
    }

    /// <summary>Gets the clock.</summary>
    internal ManualTimeProvider Time { get; }

    /// <summary>Gets the Demo state.</summary>
    internal DemoWorld World { get; }

    /// <summary>Gets the handler that lets a test answer chosen requests itself.</summary>
    internal InterceptHandler Interceptor { get; }

    /// <summary>Gets the request recorder.</summary>
    internal RecordingHandler Recorder { get; }

    /// <summary>Gets the API client.</summary>
    internal OmpApiClient Api { get; }

    /// <summary>Gets the user event feed.</summary>
    internal UserEventFeed Feed { get; }

    /// <summary>Gets the shared HTTP client.</summary>
    internal HttpClient Http => _http;

    /// <summary>Advances the clock in small steps and yields until a condition holds.</summary>
    /// <param name="done">The condition.</param>
    /// <param name="step">How far each step advances the clock, 40 ms by default.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    /// <exception cref="TimeoutException">The condition did not hold after 6000 steps.</exception>
    internal async Task PumpUntilAsync(Func<bool> done, TimeSpan? step = null)
    {
        ArgumentNullException.ThrowIfNull(done);
        for (var index = 0; index < 6000; index++)
        {
            if (done())
            {
                return;
            }

            Time.Advance(step ?? TimeSpan.FromMilliseconds(40));
            for (var yields = 0; yields < 4; yields++)
            {
                await Task.Yield();
            }

            // The scripted turn runs on the thread pool through the in-memory HTTP handler: on a loaded host the yields alone
            // let the virtual clock race ahead of work that has not been scheduled yet, so each step also gives real time.
            await Task.Delay(1, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        throw new TimeoutException("The condition did not hold after 6000 clock steps.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
        _demo.Dispose();
        World.Dispose();
    }
}
