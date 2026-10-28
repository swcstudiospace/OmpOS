using Microsoft.Extensions.Logging;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Harness.OmpLauncher;
using OmpOS.Harness.Terminal;
using OmpOS.Native.Settings.Models;
using OmpOS.Presentation;

namespace OmpOS.Native.Agents;

/// <summary>What <see cref="AgentHead.Create"/> needs: the three roots, and the seams a test replaces. The production head is built from the process environment instead (<see cref="AgentHead.CreateProduction()"/>).</summary>
internal sealed class AgentHeadOptions
{
    /// <summary>Gets the plugins root. It may be missing: the plugin stages then report Unavailable with the fix.</summary>
    public required string PluginsRoot { get; init; }

    /// <summary>Gets the root of all session state.</summary>
    public required string StateRoot { get; init; }

    /// <summary>Gets the workspace root every session runs in. It must exist.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Gets the tool kinds the approval policy admits. The production kinds by default.</summary>
    public IReadOnlyCollection<string> AllowedKinds { get; init; } = Program.ProductionAllowedKinds;

    /// <summary>Gets the transport factory of every session, or null to spawn <c>omp acp</c>.</summary>
    public Func<AcpClientOptions, IAcpTransport>? TransportFactory { get; init; }

    /// <summary>Gets the clock, or null for the system clock.</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>
    /// Gets the lookup of the other environment variables the agent loop reads (the Substrate address and token variables), or null to
    /// read none, which leaves the Substrate disabled. The plugins root and the state root are taken from this options object, not from here.
    /// </summary>
    public Func<string, string?>? GetVariable { get; init; }

    /// <summary>Gets the logger factory, or null to discard log output.</summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>Gets the omp executable, or null to resolve <c>omp</c> from <c>PATH</c>.</summary>
    public string? OmpPath { get; init; }

    /// <summary>Gets the motion setting every session carries.</summary>
    public MotionPreference Motion { get; init; } = MotionPreference.System;

    /// <summary>Gets the handler of the Substrate HTTP client, or null for a real client. The handler stays owned by the caller.</summary>
    public HttpMessageHandler? SubstrateHandler { get; init; }

    /// <summary>Gets how often the maintenance timer runs, or null for the default of 15 seconds.</summary>
    public TimeSpan? MaintenanceInterval { get; init; }

    /// <summary>Gets the hook that runs a registry change on the context that owns the models, or null to run it inline.</summary>
    public Action<Action>? Dispatch { get; init; }

    /// <summary>Gets a value indicating whether <see cref="AgentHead.InitializeAsync"/> selects the most recent saved session, which opens it. The default is true; a test that has no UI thread turns it off.</summary>
    public bool SelectMostRecentSession { get; init; } = true;

    /// <summary>Gets how a terminal session starts omp, or null for the Linux pseudo-terminal backend.</summary>
    public ITerminalBackend? TerminalBackend { get; init; }

    /// <summary>Gets how omp is located for a terminal session, or null to search <c>PATH</c> and the known install places, after <see cref="OmpPath"/> when that is set.</summary>
    public OmpLocatorOptions? TerminalLocator { get; init; }

    /// <summary>Gets how long a steering message waits for omp to acknowledge it, or null for the default of 3 seconds.</summary>
    public TimeSpan? SteerAckTimeout { get; init; }

    /// <summary>Gets the intervals of the steering channels, or null for the defaults.</summary>
    public FileSteeringChannelOptions? SteeringOptions { get; init; }

    /// <summary>Gets how often the board reads the Substrate fields of its cards, or null for the default of 30 seconds. An infinite or non-positive value starts no timer.</summary>
    public TimeSpan? DetailsRefreshInterval { get; init; }

    /// <summary>
    /// Gets whether the composer of a chat session delivers a steer or a follow-up into the running turn at once: through omp's own
    /// <c>_session/steering</c> when omp supports it, otherwise through the omp steering inbox. The default is true. When false a steer or
    /// follow-up waits for the end of the running turn and is sent as the next prompt.
    /// </summary>
    public bool LiveSteering { get; init; } = true;

    /// <summary>
    /// Gets the provider API keys that are merged into the environment of every omp the head starts, or null for none. The Models page of the
    /// settings window and this value are built together by <see cref="ModelsSettingsComposition"/>, so a key saved there reaches omp.
    /// </summary>
    public ProviderKeyEnvironment? ProviderKeys { get; init; }

    /// <summary>Gets how the Models page of the Settings window is built, or null for a window without one. The production head builds it over <see cref="ModelsSettingsComposition"/> instead.</summary>
    public Func<OmpOS.Presentation.Settings.ISettingsPage>? ModelsPageFactory { get; init; }

    /// <summary>
    /// Gets how often the Runtime settings (idle stop and retention) are applied to the sessions, or null for a head that runs no sweeper, so
    /// the Runtime settings are saved but applied to nothing (the default, which keeps tests free of a timer). The production head sets five minutes.
    /// An infinite or non-positive value runs the sweeper without a timer: it applies the settings when they are saved and once at the start.
    /// </summary>
    public TimeSpan? RuntimeSweepInterval { get; init; }

    /// <summary>Gets the head's shared services (hooks, run mode, MCP catalog, remote environments, checkpoints), or null for a head without them, which composes every session as it always did. The head disposes them.</summary>
    public OmpOS.Native.Composition.HeadServices? Services { get; init; }
}
