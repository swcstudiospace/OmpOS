using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Tests.Settings;

/// <summary>
/// A scripted diagnostics source of the Network page. A run returns the report the test set, fails with the scripted reason, or waits on a gate
/// until the test releases it or the run is cancelled. Nothing is sent anywhere.
/// </summary>
internal sealed class FakeNetworkSource : INetworkDiagnosticsSource
{
    private static readonly DateTimeOffset Finished = new(2026, 10, 26, 9, 30, 0, TimeSpan.Zero);

    /// <summary>Gets or sets the report a run returns.</summary>
    public NetworkDiagnosticsReport Report { get; set; } = new(
    [
        new EndpointCheck("openrouter", "openrouter.ai", EndpointStatus.Reachable, 401, TimeSpan.FromMilliseconds(120), "The endpoint answered with HTTP 401."),
        new EndpointCheck("ollama-local", "localhost:11434", EndpointStatus.Unreachable, null, TimeSpan.FromMilliseconds(3), "The name did not resolve, or the connection was refused or lost."),
    ],
    Finished);

    /// <summary>Gets or sets the reason every run fails with, or null for runs that succeed.</summary>
    public string? Failure { get; set; }

    /// <summary>Gets or sets a gate; while it is set, a run does not finish until the test completes it or cancels the run.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets how many runs were started.</summary>
    public int RunCount { get; private set; }

    /// <inheritdoc />
    public async Task<NetworkDiagnosticsReport> RunAsync(CancellationToken cancellationToken)
    {
        RunCount++;
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        return Failure is { } failure ? throw new SettingsSourceException(failure) : Report;
    }
}
