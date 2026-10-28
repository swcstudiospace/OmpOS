using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Safety;
using OmpOS.Harness.Remote;

namespace OmpOS.Native.Engine;

/// <summary>
/// What a head adds to the composition of one session's engine beyond its launch plan: the MCP servers the user's own files offer, the hook gate, the run mode and, for a
/// session on an ssh host, the remote resources. A session without any of them is composed exactly as it always was.
/// </summary>
internal sealed record EngineCompositionOptions
{
    /// <summary>
    /// Gets the MCP servers the session is created with after the plan's own servers (see <c>McpServerCatalog.OfferedServers</c>). A server whose name the plan already uses is left out:
    /// the plan wins, because the ACP names of one session must be unique.
    /// </summary>
    public IReadOnlyList<AcpMcpServer> ExtraMcpServers { get; init; } = [];

    /// <summary>Gets the gate through which hooks see the session's traffic, or null when hooks are off for the session.</summary>
    public IAcpHookGate? HookGate { get; init; }

    /// <summary>
    /// Gets the run mode policy in force, read for every permission request, or null for a session without run modes. When it is set, the resolver that asks the user is wrapped in
    /// <see cref="RunModePermissionResolver"/>, and in <see cref="RunMode.RunEverything"/> the approval policy admits every tool kind.
    /// </summary>
    public Func<RunModePolicy>? RunModePolicy { get; init; }

    /// <summary>Gets the sink that records every run mode decision, or null.</summary>
    public IRunModeAuditSink? RunModeAudit { get; init; }

    /// <summary>
    /// Gets the resources of a session on an ssh host, or null for a local session. The engine then connects through the resources' transport factory, resumes the conversation after a lost
    /// connection and is wrapped in a <see cref="OmpOS.EngineHost.ReconnectingEngineHost"/>. The caller owns the resources and disposes them after the engine.
    /// </summary>
    public RemoteSessionResources? Remote { get; init; }

    /// <summary>Gets the clock behind the run mode audit and the reconnect delays, or null for the system clock.</summary>
    public TimeProvider? Clock { get; init; }
}
