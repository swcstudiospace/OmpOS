using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Steering;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Terminal;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Approvals;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Composer.Attachments;
using Autonogrammer.Presentation.Editor;
using Autonogrammer.Presentation.Mentions;
using Autonogrammer.Presentation.SideChats;

namespace Autonogrammer.Native.Views.Sessions;

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
    /// Gets the lookup that tells whether a model reads images (the model's input kinds, from the model catalogue), given the id <see cref="Models"/> reports as current, or null when no model information is available.
    /// The composer's image attachments combine it with what the engine said in <c>initialize</c>: a model that lists text input only refuses images, a model the lookup does not know (<see cref="VisionSupport.Unknown"/>) accepts them with a caution.
    /// </summary>
    public Func<string, VisionSupport>? ModelVision { get; init; }

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
    /// composer a slash palette that lists them next to the Autonogrammer catalogue; a terminal runtime never does, because its omp shows its own commands.
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

    /// <summary>
    /// Gets the relay that carries the engine-side signals of the session (hook notices, the connection of a remote session, an interrupted turn) to the runtime, or null
    /// when the session has none. The runtime republishes them as <see cref="AgentSessionRuntime.HookNoticeRaised"/>, <see cref="AgentSessionRuntime.ConnectionChanged"/> and <see cref="AgentSessionRuntime.TurnInterrupted"/>.
    /// </summary>
    public SessionLiveEvents? LiveEvents { get; init; }

    /// <summary>
    /// Gets the factory of the @-mention sources of a chat session, or null when the session offers no mentions. The runtime calls it once, when it is created, and builds the
    /// composer's mention picker, its chips and its context ring over the sources; a terminal runtime never calls it. The factory does no work of its own: the sources read lazily.
    /// </summary>
    public Func<IReadOnlyList<IMentionSource>>? MentionSources { get; init; }

    /// <summary>Gets the forker of the session (a new session that continues from the same history), or null when the session cannot be forked.</summary>
    public ISessionForker? Forker { get; init; }

    /// <summary>Gets the engine of the session's side chats (a child conversation separate from the session), or null when the session offers none.</summary>
    public ISideChatEngine? SideChatEngine { get; init; }

    /// <summary>
    /// Gets the function that returns the file system of the ssh host a remote session runs on (it connects to the host the first time it is called), or null for a local session. The explorer and the editor of a remote
    /// session read and write through it.
    /// </summary>
    public Func<CancellationToken, Task<IAsyncWorkspaceFileSystem>>? RemoteFiles { get; init; }
}
