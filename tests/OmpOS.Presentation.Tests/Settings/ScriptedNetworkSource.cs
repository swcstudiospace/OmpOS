using OmpOS.Presentation.Settings;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>
/// A diagnostics source the test scripts: a run returns the report it holds, throws what it is told to, or waits on a gate until the test
/// completes it or the run is cancelled. Nothing is sent anywhere.
/// </summary>
internal sealed class ScriptedNetworkSource : INetworkDiagnosticsSource
{
    /// <summary>Gets or sets the report a run returns.</summary>
    internal NetworkDiagnosticsReport Report { get; set; } = new(
    [
        new EndpointCheck("openrouter", "openrouter.ai", EndpointStatus.Reachable, 401, TimeSpan.FromMilliseconds(120), "The endpoint answered with HTTP 401."),
        new EndpointCheck("ollama-local", "localhost:11434", EndpointStatus.Unreachable, null, null, "The name did not resolve, or the connection was refused or lost."),
        new EndpointCheck("slow", "slow.example", EndpointStatus.TimedOut, null, TimeSpan.FromSeconds(5), "The endpoint did not answer in time."),
    ],
    new DateTimeOffset(2026, 10, 26, 9, 30, 0, TimeSpan.Zero));

    /// <summary>Gets or sets what every run throws, or null for runs that succeed.</summary>
    internal Exception? Fault { get; set; }

    /// <summary>Gets or sets a gate; while it is set, a run waits for the test to complete it or for the run to be cancelled.</summary>
    internal TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets how many runs were started.</summary>
    internal int RunCount { get; private set; }

    /// <inheritdoc/>
    public async Task<NetworkDiagnosticsReport> RunAsync(CancellationToken cancellationToken)
    {
        RunCount++;
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        return Fault is { } fault ? throw fault : Report;
    }
}
