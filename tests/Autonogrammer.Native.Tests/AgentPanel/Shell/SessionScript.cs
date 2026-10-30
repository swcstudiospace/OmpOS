using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Steering;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.Parity;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Terminal;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Approvals;
using Autonogrammer.Presentation.Mentions;
using Autonogrammer.Presentation.SideChats;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>What one session of a shell under test is made of: its send path, its approval bridge, its stage source and its task context.</summary>
internal sealed class SessionScript
{
    /// <summary>Gets the send path of the session. An <see cref="UnconnectedPromptChannel"/> refuses every send.</summary>
    public IPromptChannel Prompts { get; init; } = new UnconnectedPromptChannel();

    /// <summary>Gets the bridge that records the session's tool-approval decisions.</summary>
    public IToolApprovalBridge Approvals { get; init; } = new UnconnectedToolApprovalBridge();

    /// <summary>Gets the source of the session's stage events.</summary>
    public ScriptedStageSource Stages { get; init; } = new();

    /// <summary>Gets the source of the session's task context.</summary>
    public ITaskContextSource Context { get; init; } = new ScriptedTaskContext(null);

    /// <summary>Gets the callback that records a steering entry, or null.</summary>
    public Func<SteeringEntry, CancellationToken, Task>? RecordSteering { get; init; }

    /// <summary>Gets the steering channel of the session, or null to queue messages sent during a run for the end of the turn.</summary>
    public ISteeringChannel? Steering { get; init; }

    /// <summary>Gets the starter of the omp terminal of the session, or null for a chat session.</summary>
    public IOmpTerminalStarter? TerminalStarter { get; init; }

    /// <summary>Gets the slash commands the session's engine advertises, or null for a session without a slash palette.</summary>
    public IOmpSessionCommands? SessionCommands { get; init; }

    /// <summary>Gets the sources of the session's <c>@</c> mentions, or null for a session without mentions.</summary>
    public Func<IReadOnlyList<IMentionSource>>? MentionSources { get; init; }

    /// <summary>Gets the forker of the session, or null for a session that cannot fork.</summary>
    public ISessionForker? Forker { get; init; }

    /// <summary>Gets the live signals of the session (hook notices, connection changes, interrupted turns), or null.</summary>
    public SessionLiveEvents? LiveEvents { get; init; }

    /// <summary>Gets the scripted channel when the send path is one, or fails the test.</summary>
    public ScriptedPromptChannel Scripted => (ScriptedPromptChannel)Prompts;

    /// <summary>Creates a session script over scripted turns.</summary>
    /// <param name="turns">The turns the session's send path plays, one per prompt.</param>
    /// <returns>The script.</returns>
    public static SessionScript WithTurns(params ScriptedTurn[] turns) => new() { Prompts = new ScriptedPromptChannel(turns) };
}
