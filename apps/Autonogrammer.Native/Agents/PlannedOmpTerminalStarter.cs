using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Terminal;
using Autonogrammer.Native.Settings.Models;
using Autonogrammer.Native.Views.Terminal;

namespace Autonogrammer.Native.Agents;

/// <summary>
/// Starts the omp of a terminal session from the session's launch plan: the plan's plugin arguments, environment and workspace become the
/// request of the <see cref="OmpTerminalLauncher"/>, so an app-started omp terminal loads the same plugins from the same session state as
/// the engine behind the Agent Panel. The plan is decided once per session (the Substrate health check is asynchronous), the first time a
/// start asks for it, and a restart reuses it. A resume reference is passed to omp only when the registry holds one. The provider API keys
/// of the providers Autonogrammer wrote to <c>models.yml</c> are read at every start and added to the request's environment after the plan, so the
/// plan never holds a key and a restart sees a key saved since.
/// </summary>
internal sealed class PlannedOmpTerminalStarter : IOmpTerminalStarter
{
    private readonly SessionSlot _slot;
    private readonly Func<string?> _resumeReference;
    private readonly OmpTerminalLauncher _launcher;
    private readonly ProviderKeyEnvironment? _providerKeys;

    /// <summary>Creates the starter of one terminal session.</summary>
    /// <param name="slot">The slot of the session, which decides the plan.</param>
    /// <param name="resumeReference">Reads the native omp session to resume at each start, or null for a new omp session. Called on every start.</param>
    /// <param name="launcher">Locates omp and opens the pseudo-terminal.</param>
    /// <param name="providerKeys">The provider API keys to add to the environment of omp, or null for none.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public PlannedOmpTerminalStarter(SessionSlot slot, Func<string?> resumeReference, OmpTerminalLauncher launcher, ProviderKeyEnvironment? providerKeys = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(resumeReference);
        ArgumentNullException.ThrowIfNull(launcher);
        _slot = slot;
        _resumeReference = resumeReference;
        _launcher = launcher;
        _providerKeys = providerKeys;
    }

    /// <inheritdoc />
    public async Task<OmpTerminalLaunchResult> StartAsync(int columns, int rows, IProcessOutputSink output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        SessionPlan plan;
        try
        {
            plan = await _slot.PlanAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            return new OmpTerminalLaunchFailed($"The omp terminal could not be prepared: {exception.Message}");
        }

        var command = plan.Launch.ForTerminal(new OmpTerminalOptions { NativeSessionId = _resumeReference() });
        var environment = command.Environment;
        if (_providerKeys is not null)
        {
            await _providerKeys.RefreshAsync(cancellationToken).ConfigureAwait(false);
            environment = _providerKeys.Merge(environment);
        }

        var request = new OmpTerminalLaunchRequest(command.WorkingDirectory, command.Arguments, columns, rows, output)
        {
            Environment = environment,
        };
        return await _launcher.LaunchAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
