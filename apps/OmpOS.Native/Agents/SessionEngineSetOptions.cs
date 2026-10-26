using Microsoft.Extensions.Logging;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Presentation;

namespace OmpOS.Native.Agents;

/// <summary>What the head supplies to <see cref="SessionEngineSet"/>: where things are, which tools the approval policy admits, and the seams a test replaces.</summary>
internal sealed class SessionEngineSetOptions
{
    /// <summary>The longest time the Substrate health check of a new session may take before the Substrate is left out of it.</summary>
    public static readonly TimeSpan DefaultHealthTimeout = TimeSpan.FromSeconds(3);

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
}
