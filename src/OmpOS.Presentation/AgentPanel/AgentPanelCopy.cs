using System.Collections.Frozen;
using OmpOS.Contracts.Agents;
using OmpOS.Presentation.Runs;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// Every user-facing sentence of the Agent Panel, exactly as the Copywriting Contract of 46-UI-SPEC words it. Views and tests read the
/// same constants, so a copy change is made in one place. Text that depends on a value is built by a method here and never by a view.
/// </summary>
public static class AgentPanelCopy
{
    private static readonly FrozenDictionary<(StageSource Source, StageStatus Status), string> Effects = BuildEffects();

    /// <summary>The primary call to action in the rail and the empty state.</summary>
    public const string NewAgent = "New agent";

    /// <summary>The rail control that creates an omp terminal session beside New agent.</summary>
    public const string NewTerminal = "New omp terminal";

    /// <summary>The tooltip of the New omp terminal control, with its shortcut.</summary>
    public const string NewTerminalTooltip = "New omp terminal (Ctrl+Shift+T)";

    /// <summary>The accessible name of the message box of the steer-only composer of an omp terminal session.</summary>
    public const string TerminalMessageName = "Steer message";

    /// <summary>The accessible name of the Send now control of an omp terminal session.</summary>
    public const string SendNowName = "Send now, deliver the message into the running omp";

    /// <summary>The accessible name of the Queue control of an omp terminal session.</summary>
    public const string QueueFollowUpName = "Queue, run the message after the current turn";

    /// <summary>The placeholder of the steer-only composer of an omp terminal session.</summary>
    public const string ComposerPlaceholderTerminal = "Steer this omp terminal";

    /// <summary>The label of the control that delivers a steering message into the running turn at once.</summary>
    public const string SendNow = "Send now";

    /// <summary>The label of the control that queues a message for the end of the running turn.</summary>
    public const string QueueFollowUpLabel = "Queue";

    /// <summary>The tag a terminal session shows in the rail and on its header.</summary>
    public const string TerminalTag = "Terminal";

    /// <summary>The words that name a terminal session inside its accessible name, in running text.</summary>
    public const string TerminalAccessibleTag = "terminal";

    /// <summary>The status line text when a message is sent to an omp terminal that is not running.</summary>
    public const string TerminalNotRunning = "This omp terminal is not running. Restart omp, then send the message again.";

    /// <summary>The accessible name of the primary button of the empty thread.</summary>
    public const string StartFirstAgent = "Start first agent";

    /// <summary>The composer placeholder while the agent is idle.</summary>
    public const string ComposerPlaceholderIdle = "Describe a task for this agent\u2026";

    /// <summary>The composer placeholder while a run is active.</summary>
    public const string ComposerPlaceholderRunning = "Steer the running agent\u2026 It is delivered after the active turn.";

    /// <summary>The composer placeholder while a run is active and its steering is delivered live into the running turn.</summary>
    public const string ComposerPlaceholderRunningLive = "Steer the running agent\u2026 Send now delivers it into the running turn.";

    /// <summary>The accessible name of the send control of a composer whose steering is delivered live into the running turn.</summary>
    public const string ComposerSendNowName = "Send now, deliver the message into the running turn";

    /// <summary>The pending line of a composer whose steering is delivered live, while a run is active and nothing is pending.</summary>
    public const string ComposerPendingActiveLive = "A run is active. Send now delivers into the running turn; Queue follow-up runs after the run completes.";

    /// <summary>The placeholder of the rail filter box.</summary>
    public const string FilterPlaceholder = "Filter agents\u2026";

    /// <summary>The heading of the loading state of the panel.</summary>
    public const string PanelLoadingHeading = "Starting the agent panel\u2026";

    /// <summary>The body of the loading state of the panel.</summary>
    public const string PanelLoadingBody = "Checking plugins and restoring your agents.";

    /// <summary>The rail text while the agents load.</summary>
    public const string RailLoading = "Loading agents\u2026";

    /// <summary>The heading of the empty rail.</summary>
    public const string EmptyRailHeading = "No agents yet.";

    /// <summary>The body of the empty rail.</summary>
    public const string EmptyRailBody = "Start an agent to work on a task.";

    /// <summary>The heading of the empty thread.</summary>
    public const string EmptyThreadHeading = "Start your first agent";

    /// <summary>The body of the empty thread.</summary>
    public const string EmptyThreadBody =
        "Describe a task. OmpOS plans it with the plugins that are available and falls back to a single agent.";

    /// <summary>The heading of a selected session with no messages.</summary>
    public const string EmptySessionHeading = "What should this agent do?";

    /// <summary>The body of a selected session with no messages.</summary>
    public const string EmptySessionBody = "Send a task. The pipeline starts when you send it.";

    /// <summary>The label of the retry button of the panel error.</summary>
    public const string RetryLoading = "Retry loading";

    /// <summary>The accessible name of the retry button of the panel error.</summary>
    public const string RetryLoadingName = "Retry loading agents";

    /// <summary>The heading of the rail while the panel is in error.</summary>
    public const string RailErrorHeading = "Could not load agents.";

    /// <summary>The body of the rail while the panel is in error.</summary>
    public const string RailErrorBody = "Your saved agents are untouched. Use Retry loading above.";

    /// <summary>The label of the stage retry button.</summary>
    public const string RetryStage = "Retry stage";

    /// <summary>The panel notice when no plugin is available.</summary>
    public const string NoPlugins = "No plugins are available. This agent runs as plain omp.";

    /// <summary>The heading of a lost session.</summary>
    public const string LostHeading = "This agent was running when OmpOS closed.";

    /// <summary>The body of a lost session.</summary>
    public const string LostBody = "Resume it to continue from its last saved point.";

    /// <summary>The label of the resume button.</summary>
    public const string ResumeAgent = "Resume agent";

    /// <summary>The panel notice after a session was resumed.</summary>
    public const string Resumed = "Resumed. The agent continues from its last saved point.";

    /// <summary>The announcement after a session was resumed.</summary>
    public const string ResumedAnnouncement = "Resumed.";

    /// <summary>The heading of a closed session.</summary>
    public const string ClosedHeading = "This agent is closed.";

    /// <summary>The body of a closed session.</summary>
    public const string ClosedBody = "Its engine has stopped. Reopen it to continue, or start a new agent.";

    /// <summary>The label of the reopen button.</summary>
    public const string ReopenAgent = "Reopen agent";

    /// <summary>The announcement that a run finished.</summary>
    public const string AgentFinished = "Agent finished.";

    /// <summary>The text of the stop confirmation bar.</summary>
    public const string StopConfirmation = "Stop this agent? The current turn ends. Partial output stays in the thread.";

    /// <summary>The label of the stop confirmation.</summary>
    public const string StopAgent = "Stop agent";

    /// <summary>The label that dismisses the stop confirmation.</summary>
    public const string KeepRunning = "Keep running";

    /// <summary>The label of the close confirmation.</summary>
    public const string CloseAgent = "Close agent";

    /// <summary>The label that dismisses the close confirmation.</summary>
    public const string KeepAgentOpen = "Keep agent open";

    /// <summary>The status line text when nothing runs and the user presses Stop.</summary>
    public const string NothingToStop = "No response is running.";

    /// <summary>The heading of the empty steering history.</summary>
    public const string SteeringEmptyHeading = "No steering yet.";

    /// <summary>The body of the empty steering history.</summary>
    public const string SteeringEmptyBody = "Messages you send while an agent runs appear here.";

    /// <summary>The title of a session whose title is blank.</summary>
    public const string UntitledAgent = "Untitled agent";

    /// <summary>The title of a task whose title is blank.</summary>
    public const string UntitledTask = "Untitled task";

    /// <summary>The line shown for a source with no events.</summary>
    public const string WaitingForEvents = "Waiting for events.";

    /// <summary>The caption of a workflow phase that could not be read.</summary>
    public const string PhaseUnknown = "Phase unknown";

    /// <summary>The heading while the task context loads.</summary>
    public const string ContextLoadingHeading = "Loading task context\u2026";

    /// <summary>The heading when the agent is bound to no task.</summary>
    public const string ContextNoTaskHeading = "No task for this agent.";

    /// <summary>The body when the agent is bound to no task.</summary>
    public const string ContextNoTaskBody =
        "This agent is not bound to a task. Bind one to see its status, phase, shared memory and lessons.";

    /// <summary>The heading when Substrate is off.</summary>
    public const string ContextLimitedHeading = "Task context is limited.";

    /// <summary>The body when Substrate is off.</summary>
    public const string ContextLimitedBody =
        "Shared memory and lessons need Substrate, which is not reachable. The workflow phase is still shown when GSD tools are present.";

    /// <summary>The heading when the task context cannot be shown for another reason.</summary>
    public const string ContextUnavailableHeading = "Task context is unavailable.";

    /// <summary>The body when the task context is unavailable and no warning explains it.</summary>
    public const string ContextUnavailableBody = "Try again after the agent finishes its next turn.";

    /// <summary>The body of a partially read task context.</summary>
    public const string ContextPartialBody = "Some context could not be read.";

    /// <summary>The header of the warnings block.</summary>
    public const string ContextWarningsHeader = "Could not read";

    /// <summary>The text of an empty shared memory list.</summary>
    public const string MemoryEmpty = "No shared memory for this agent.";

    /// <summary>The text of an empty lessons list.</summary>
    public const string LessonsEmpty = "No lessons recorded for this task.";

    /// <summary>The delivery text of a steering message the engine accepted and will deliver after the active turn.</summary>
    public const string DeliveryQueued = "Queued after the active turn";

    /// <summary>The delivery text of a steering message the engine holds until the active turn ends.</summary>
    public const string DeliveryHeld = "Held until the active turn ends";

    /// <summary>The delivery text of a steering message the engine took at once.</summary>
    public const string DeliveryDelivered = "Delivered";

    /// <summary>The delivery text of a steering message that reached a session with no running turn: omp started a new turn from it.</summary>
    public const string DeliveryStartedNewTurn = "Delivered. The turn had already ended, so omp started a new turn from your message.";

    /// <summary>The delivery text of a steering message a live channel queued for omp while omp has not acknowledged it yet.</summary>
    public const string DeliverySentNotAcknowledged = "Sent, omp has not acknowledged it yet";

    /// <summary>The delivery text prefix of a steering message that was not delivered.</summary>
    public const string DeliveryNotDelivered = "Not delivered";

    /// <summary>The record text of a steering message Substrate accepted a note for.</summary>
    public const string RecordRecorded = "Recorded in Substrate";

    /// <summary>The record text of a steering message that was not recorded because Substrate is not reachable.</summary>
    public const string RecordNotRecorded = "Not recorded: Substrate is not reachable";

    /// <summary>The record text of a steering message whose record failed.</summary>
    public const string RecordFailed = "Record failed";

    /// <summary>The record text of a steering message whose record is in flight.</summary>
    public const string RecordInFlight = "Recording\u2026";

    /// <summary>The steering badge while a run is active and Substrate records.</summary>
    public const string SteeringAvailable = "Steering available";

    /// <summary>The steering badge while a run is active and Substrate does not record.</summary>
    public const string SteeringAvailableNotRecorded = "Steering available, not recorded";

    /// <summary>The steering badge while no run is active.</summary>
    public const string SteeringIdle = "Steering idle";

    /// <summary>The steering badge while the session cannot take steering.</summary>
    public const string SteeringUnavailable = "Steering unavailable";

    /// <summary>The heading of the pipeline strip name.</summary>
    public const string PipelineAccessibleNamePrefix = "Pipeline: ";

    /// <summary>The label of the steering kind for a steer.</summary>
    public const string KindSteer = "Steer";

    /// <summary>The label of the steering kind for a follow-up.</summary>
    public const string KindFollowUp = "Follow-up";

    /// <summary>Returns the delivery text of a message that was not delivered.</summary>
    /// <param name="reason">The plain reason, or null when none is known.</param>
    /// <returns>The text.</returns>
    public static string DeliveryFailed(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? DeliveryNotDelivered : $"{DeliveryNotDelivered}: {reason.TrimEnd('.', ' ')}";

    /// <summary>Returns the filter-matches-nothing heading.</summary>
    /// <param name="filter">The filter text.</param>
    /// <returns>The heading.</returns>
    public static string FilterNoMatchHeading(string filter) => $"No agent matches \"{filter}\".";

    /// <summary>Returns the filter-matches-nothing body.</summary>
    /// <param name="total">The number of agents the filter hides.</param>
    /// <returns>The body.</returns>
    public static string FilterNoMatchBody(int total) =>
        total == 1 ? "Clear the filter to see your 1 agent." : $"Clear the filter to see all {total} agents.";

    /// <summary>Returns the panel error text.</summary>
    /// <param name="reason">The reason. Trailing full stops are removed.</param>
    /// <returns>The sentence.</returns>
    public static string PanelError(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return $"Could not load your agents. {reason.TrimEnd('.', ' ')}. Your saved agents are not lost.";
    }

    /// <summary>Returns the text of an action that failed.</summary>
    /// <param name="action">What the user tried, for example <c>start a new agent</c>.</param>
    /// <param name="reason">The reason. Trailing full stops are removed.</param>
    /// <returns>The sentence.</returns>
    public static string ActionFailed(string action, string reason) =>
        string.IsNullOrWhiteSpace(reason) ? $"Could not {action}." : $"Could not {action}. {reason.TrimEnd('.', ' ')}.";

    /// <summary>Returns the degraded-stage panel notice.</summary>
    /// <param name="count">The number of degraded stages. At least 1.</param>
    /// <returns>The notice.</returns>
    public static string DegradedNotice(int count) =>
        count == 1
            ? "1 pipeline stage is degraded. The agent keeps running."
            : $"{count} pipeline stages are degraded. The agent keeps running.";

    /// <summary>Returns the close confirmation text.</summary>
    /// <param name="title">The agent title.</param>
    /// <returns>The text.</returns>
    public static string CloseConfirmation(string title) =>
        $"Close \"{title}\"? The agent is running. Closing stops its engine and ends the run. Partial output is kept.";

    /// <summary>Returns the announcement that an agent was closed.</summary>
    /// <param name="title">The agent title.</param>
    /// <returns>The announcement.</returns>
    public static string ClosedAnnouncement(string title) => $"Agent \"{title}\" closed.";

    /// <summary>Returns the announcement that re-checking a source failed.</summary>
    /// <param name="sourceName">The display name of the source.</param>
    /// <returns>The announcement.</returns>
    public static string RecheckFailed(string sourceName) => $"Could not re-check {sourceName}.";

    /// <summary>Returns the message shown when the queue is full.</summary>
    /// <param name="limit">The queue limit, <see cref="RunInputScheduler.MaxQueuedMessages"/>.</param>
    /// <returns>The message.</returns>
    public static string QueueFull(int limit) =>
        $"The queue holds {limit} messages. Wait for the active turn to end, then send again.";

    /// <summary>Returns the held-steering header.</summary>
    /// <param name="held">The number of held steering messages.</param>
    /// <returns>The text, or null when none is held.</returns>
    public static string? HeldSteering(int held) => held switch
    {
        <= 0 => null,
        1 => "1 steering message is held until the active turn ends.",
        _ => $"{held} steering messages are held until the active turn ends.",
    };

    /// <summary>Returns the accessible name of the steering badge.</summary>
    /// <param name="badgeText">The badge text.</param>
    /// <returns>The name.</returns>
    public static string SteeringBadgeName(string badgeText) =>
        $"Steering availability: {badgeText}. Messages are delivered after the active turn.";

    /// <summary>Returns the accessible name of the steering badge of a session whose steering is delivered live.</summary>
    /// <param name="badgeText">The badge text.</param>
    /// <returns>The name.</returns>
    public static string LiveSteeringBadgeName(string badgeText) =>
        $"Steering availability: {badgeText}. A steer is sent into the running turn; a queued message runs after it.";

    /// <summary>Returns the words of a stage status.</summary>
    /// <param name="status">The status. An undefined value reads <c>Unknown</c>.</param>
    /// <returns>The status word, never colour alone.</returns>
    public static string StageStatusText(StageStatus status) => status switch
    {
        StageStatus.Pending => "Waiting",
        StageStatus.Running => "Running",
        StageStatus.Succeeded => "Done",
        StageStatus.Degraded => "Degraded",
        StageStatus.Failed => "Failed",
        StageStatus.Skipped => "Skipped",
        StageStatus.Unavailable => "Unavailable",
        _ => "Unknown",
    };

    /// <summary>Returns the glyph key of a stage status.</summary>
    /// <param name="status">The status. An undefined value gets the info glyph.</param>
    /// <returns>The glyph key from <see cref="StatusIcon"/>.</returns>
    public static string StageStatusIcon(StageStatus status) => status switch
    {
        StageStatus.Pending => StatusIcon.Clock,
        StageStatus.Running => StatusIcon.Running,
        StageStatus.Succeeded => StatusIcon.Check,
        StageStatus.Degraded => StatusIcon.Alert,
        StageStatus.Failed => StatusIcon.Cross,
        StageStatus.Skipped => StatusIcon.Closed,
        StageStatus.Unavailable => StatusIcon.Slash,
        _ => StatusIcon.Info,
    };

    /// <summary>Returns the effect line of a stage, which says what the status means for the run.</summary>
    /// <param name="source">The source of the stage.</param>
    /// <param name="status">The status of the stage.</param>
    /// <returns>The effect line, or null when the status has none.</returns>
    public static string? StageEffect(StageSource source, StageStatus status) =>
        Effects.TryGetValue((source, status), out var line) ? line : null;

    private static FrozenDictionary<(StageSource Source, StageStatus Status), string> BuildEffects()
    {
        var effects = new Dictionary<(StageSource Source, StageStatus Status), string>
        {
            [(StageSource.Omp, StageStatus.Degraded)] = "The engine is running with reduced function.",
            [(StageSource.Omp, StageStatus.Failed)] = "The engine failed. See the error above the transcript.",
            [(StageSource.Omp, StageStatus.Unavailable)] = "The omp engine is not available.",
            [(StageSource.Ultrathink, StageStatus.Skipped)] = "Planning is off. Prompts run as written.",
            [(StageSource.Ultrathink, StageStatus.Degraded)] = "The planner fell back to a plain spec. No graph was created.",
            [(StageSource.Ultrathink, StageStatus.Failed)] = "Planning failed. The prompt ran as written.",
            [(StageSource.Ultrathink, StageStatus.Unavailable)] = "Ultrathink is not available. Prompts run as written.",
            [(StageSource.Swarm, StageStatus.Skipped)] = "Swarm is off. This agent runs alone.",
            [(StageSource.Swarm, StageStatus.Degraded)] = "Swarm is running with reduced function.",
            [(StageSource.Swarm, StageStatus.Failed)] = "Swarm stopped. Work continues as a single agent.",
            [(StageSource.Swarm, StageStatus.Unavailable)] = "Swarm is not available. This agent runs alone.",
            [(StageSource.Substrate, StageStatus.Skipped)] = "Substrate is off. Steering is not recorded and shared memory is off.",
            [(StageSource.Substrate, StageStatus.Degraded)] =
                "Substrate answered with errors. Some steering records or memory may be missing.",
            [(StageSource.Substrate, StageStatus.Failed)] = "Substrate failed. Steering is not recorded.",
            [(StageSource.Substrate, StageStatus.Unavailable)] =
                "Substrate is not reachable. Steering is not recorded and shared memory is off.",
            [(StageSource.Gsd, StageStatus.Skipped)] = "GSD is off. The workflow phase is unknown.",
            [(StageSource.Gsd, StageStatus.Degraded)] = "GSD data is partial.",
            [(StageSource.Gsd, StageStatus.Failed)] = "GSD could not read the phase. The workflow phase is unknown.",
            [(StageSource.Gsd, StageStatus.Unavailable)] = "GSD tools were not found. The workflow phase is unknown.",
        };
        return effects.ToFrozenDictionary();
    }
}
