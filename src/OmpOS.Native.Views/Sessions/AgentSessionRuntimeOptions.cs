using OmpOS.Contracts.Agents;
using OmpOS.Native.Engine;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;

namespace OmpOS.Native.Views.Sessions;

/// <summary>What a head supplies to build the <see cref="AgentSessionRuntime"/> of one agent session.</summary>
public sealed class AgentSessionRuntimeOptions
{
    /// <summary>Gets the rail row of the session, which names it and gives its status.</summary>
    public required AgentRow Row { get; init; }

    /// <summary>Gets the send path of the session's engine. The runtime disposes it once when the runtime is disposed.</summary>
    public required IPromptChannel Prompts { get; init; }

    /// <summary>Gets the bridge that records each tool-approval decision of the session.</summary>
    public required IToolApprovalBridge Approvals { get; init; }

    /// <summary>Gets the scope the stage sources and the context source read: only state that belongs to this session.</summary>
    public required StageObservationScope Scope { get; init; }

    /// <summary>Gets the source of the session's stage events.</summary>
    public required IStageSource Stages { get; init; }

    /// <summary>Gets the source of the session's task context.</summary>
    public required ITaskContextSource Context { get; init; }

    /// <summary>Gets the callback that records an accepted steering entry, for example in Substrate, or null when none is bound.</summary>
    public Func<SteeringEntry, CancellationToken, Task>? RecordSteering { get; init; }

    /// <summary>Gets the callback that resumes a lost session by starting its engine again, or null when it cannot be resumed.</summary>
    public Func<CancellationToken, Task>? Resume { get; init; }

    /// <summary>Gets the callback that reopens a closed session by starting its engine again, or null when it cannot be reopened.</summary>
    public Func<CancellationToken, Task>? Reopen { get; init; }

    /// <summary>Gets the callback that re-checks a plugin source on request, or null when stages offer no retry.</summary>
    public Func<StageSource, CancellationToken, Task>? StageRecheck { get; init; }

    /// <summary>Gets the motion setting of the head. The runtime keeps it for <see cref="AgentSessionRuntime.Motion"/>; the main view's styles read it.</summary>
    public MotionPreference Motion { get; init; }

    /// <summary>Gets the clock. Null uses <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider? TimeProvider { get; init; }
}
