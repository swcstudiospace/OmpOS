using OmpOS.Contracts.Agents;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Native.Engine;
using OmpOS.Native.Views.Terminal;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Commands;

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

    /// <summary>
    /// Gets the channel that delivers a steer or a follow-up into the running omp session of the session, or null to queue messages sent
    /// during a run for the end of the turn. In a terminal runtime it is the only way the composer reaches omp.
    /// </summary>
    public ISteeringChannel? Steering { get; init; }

    /// <summary>
    /// Gets the starter of the omp terminal of the session. Non-null makes a terminal runtime: its <see cref="AgentSessionRuntime.Kind"/> is
    /// <see cref="SessionKind.Terminal"/>, the thread shows the terminal view, and <see cref="Prompts"/> is an <see cref="UnconnectedPromptChannel"/>.
    /// </summary>
    public IOmpTerminalStarter? TerminalStarter { get; init; }

    /// <summary>
    /// Gets the control that reads the models of the session's engine and switches the current one, or null when the session offers none.
    /// A chat runtime then shows a model chip in its composer; a terminal runtime never does, because its omp owns its model.
    /// </summary>
    public IOmpModelControl? Models { get; init; }

    /// <summary>
    /// Gets the callback that starts the engine session so that <see cref="Models"/> reports its models, or null when the models are known
    /// without it. The model chip calls it the first time it is opened, so a session that has not sent a prompt still lists its models.
    /// </summary>
    public Func<CancellationToken, Task>? ConnectModels { get; init; }

    /// <summary>
    /// Gets the omp live steering of the session's engine, or null when the session has none (a terminal, or a head that disables live steering).
    /// While it reports <see cref="IOmpLiveSteering.IsSupported"/>, a steer or a follow-up of a chat session goes through it and not through
    /// <see cref="Steering"/>; <see cref="Steering"/> stays the path of a terminal and the fallback of an omp that does not support it.
    /// </summary>
    public IOmpLiveSteering? NativeSteering { get; init; }

    /// <summary>
    /// Gets the select-style options (mode, thinking level) of the session's engine, or null when the session offers none. A chat runtime
    /// then shows a mode chip and a thinking chip once the engine reports those options; a terminal runtime never does.
    /// </summary>
    public IOmpSessionOptions? SessionOptions { get; init; }

    /// <summary>
    /// Gets the slash commands and skills the session's engine advertises, or null when the session offers none. A chat runtime then gives its
    /// composer a slash palette that lists them next to the OmpOS catalogue; a terminal runtime never does, because its omp shows its own commands.
    /// The palette only lists and completes names: omp runs a command when the prompt that starts with it is sent. <see cref="ConnectModels"/>
    /// starts the engine session when the palette opens before the engine has advertised its commands.
    /// </summary>
    public IOmpSessionCommands? SessionCommands { get; init; }

    /// <summary>
    /// Gets the source of the custom commands, skills and subagents of the session's workspace (<c>ConventionSlashDiscoverySource</c> reads them from every convention directory), or null when the session lists none.
    /// The runtime reads it when the session starts and again, at most every few seconds, when the slash palette opens, so a file added meanwhile appears without a restart.
    /// </summary>
    public ISlashDiscoverySource? SlashDiscovery { get; init; }

    /// <summary>
    /// Gets the parts of the head that native slash actions drive: Settings, a new chat, a new terminal, a side chat. The runtime adds the targets its own composer reaches (mode, model, help). A target the head leaves null
    /// makes its command unavailable in the palette.
    /// </summary>
    public DesktopSlashTargets? SlashTargets { get; init; }
}
