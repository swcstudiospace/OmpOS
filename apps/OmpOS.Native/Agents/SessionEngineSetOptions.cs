using Microsoft.Extensions.Logging;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Harness.OmpLauncher;
using OmpOS.Harness.Terminal;
using OmpOS.Native.Settings.Models;
using OmpOS.Presentation;

namespace OmpOS.Native.Agents;

/// <summary>What the head supplies to <see cref="SessionEngineSet"/>: where things are, which tools the approval policy admits, and the seams a test replaces.</summary>
internal sealed class SessionEngineSetOptions
{
    /// <summary>The longest time the Substrate health check of a new session may take before the Substrate is left out of it.</summary>
    public static readonly TimeSpan DefaultHealthTimeout = TimeSpan.FromSeconds(3);

    /// <summary>The longest time a steering message waits for omp to acknowledge it.</summary>
    public static readonly TimeSpan DefaultSteerAckTimeout = TimeSpan.FromSeconds(3);

    /// <summary>How often the head refreshes the heartbeat of running sessions and looks for lost ones.</summary>
    public static readonly TimeSpan DefaultMaintenanceInterval = TimeSpan.FromSeconds(15);

    /// <summary>Gets where the plugins, the state and the Substrate are found.</summary>
    public required AgentLoopEnvironment Loop { get; init; }

    /// <summary>Gets the tool kinds the approval policy of every session admits.</summary>
    public required IReadOnlyCollection<string> AllowedKinds { get; init; }

    /// <summary>Gets the clock for the registry, the planner, the observers and the maintenance timer. The system clock by default.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>Gets the transport factory of every session, or null to spawn <c>omp acp</c>.</summary>
    public Func<AcpClientOptions, IAcpTransport>? TransportFactory { get; init; }

    /// <summary>Gets the logger factory of the engines, the observers and the registry, or null to discard their output.</summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>Gets the omp executable, or null to resolve <c>omp</c> from <c>PATH</c>.</summary>
    public string? OmpPath { get; init; }

    /// <summary>Gets the motion setting every session's runtime carries.</summary>
    public MotionPreference Motion { get; init; } = MotionPreference.System;

    /// <summary>Gets the handler of the Substrate HTTP client, or null to use a client without redirects and cookies. The handler stays owned by the caller.</summary>
    public HttpMessageHandler? SubstrateHandler { get; init; }

    /// <summary>Gets the longest time a Substrate health check may take.</summary>
    public TimeSpan HealthTimeout { get; init; } = DefaultHealthTimeout;

    /// <summary>Gets how often the maintenance timer runs. A value that is not positive starts no timer.</summary>
    public TimeSpan MaintenanceInterval { get; init; } = DefaultMaintenanceInterval;

    /// <summary>Gets how a terminal session starts omp: the backend that opens the pseudo-terminal. Null uses <see cref="LinuxLocalPtyBackend"/>.</summary>
    public ITerminalBackend? TerminalBackend { get; init; }

    /// <summary>Gets how omp is located for a terminal session, or null to search <c>PATH</c> and the known install places, after <see cref="OmpPath"/> when that is set.</summary>
    public OmpLocatorOptions? TerminalLocator { get; init; }

    /// <summary>Gets the longest time a steering message waits for omp to acknowledge it before it is reported as sent but not acknowledged.</summary>
    public TimeSpan SteerAckTimeout { get; init; } = DefaultSteerAckTimeout;

    /// <summary>Gets the intervals of the steering channels of the sessions, or null for the defaults.</summary>
    public FileSteeringChannelOptions? SteeringOptions { get; init; }

    /// <summary>
    /// Gets whether the composer of a chat session delivers a steer or a follow-up through the omp steering channel at once. When false the
    /// sessions have no live channel: a steer or follow-up waits for the end of the running turn and is sent as the next prompt.
    /// </summary>
    public bool LiveSteering { get; init; } = true;

    /// <summary>
    /// Gets the provider API keys of the providers OmpOS wrote to <c>models.yml</c>, or null for none. They are merged into the environment of
    /// every omp the set starts (the engine of a chat session and the omp of a terminal session), after planning, so the planner and the plan
    /// it returns never hold a key.
    /// </summary>
    public ProviderKeyEnvironment? ProviderKeys { get; init; }

    /// <summary>
    /// Gets the head's shared services (hooks, run mode, MCP catalog, remote environments, checkpoints), or null for a head without them. Without it a session is composed exactly as it was before
    /// these features existed: no hooks, no run mode (omp keeps its own approval default), no MCP servers of the user's files, no ssh sessions.
    /// </summary>
    public OmpOS.Native.Composition.HeadServices? Services { get; init; }
}
