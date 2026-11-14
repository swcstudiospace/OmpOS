using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.Automations.Scheduling;
using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// Starts a scheduled run as a Cowork task through <see cref="CoworkSessionHost"/>: create the task, open it, then send the prompt. It accepts only
/// the Cowork surface. The task is linked to the run's project and trusts the run's folders, which the host checks against the folder boundary and
/// this device before anything is written. Approval is always the Manual mode: a scheduled run has no one to ask, Manual asks for every action, so an
/// action that needs approval waits and the runner cancels it. Auto is run as Manual because its read-only shell and allow-rule approvals would act
/// unattended. Skip is refused, because the host cannot prove the folder boundary is filesystem-isolated. A model choice is refused, because a Cowork
/// session has no model selection yet. A failure, including one after the task was created, is returned as a not-started result; a partial task is closed first.
/// </summary>
internal sealed class ScheduledCoworkSessionStarter : IScheduledSessionStarter
{
    private const string RunTitle = "Scheduled run";

    private readonly CoworkSessionHost _sessions;
    private readonly TimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="ScheduledCoworkSessionStarter"/> class.</summary>
    /// <param name="sessions">The Cowork session host the run starts in. The caller owns and disposes it.</param>
    /// <param name="clock">The clock that stamps the session row, or null for the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sessions"/> is null.</exception>
    public ScheduledCoworkSessionStarter(CoworkSessionHost sessions, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        _sessions = sessions;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ScheduledSessionSurface Surface => ScheduledSessionSurface.Cowork;

    /// <inheritdoc />
    public async Task<ScheduledSessionStart> StartAsync(
        ScheduledSessionSurface surface,
        string? projectId,
        IReadOnlyList<string> trustedFolders,
        SessionApprovalMode approvalMode,
        string? model,
        string prompt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trustedFolders);
        ArgumentNullException.ThrowIfNull(prompt);
        if (surface != Surface)
        {
            return NotStarted("This starter runs only Cowork sessions.");
        }

        if (Unsupported(approvalMode, model) is { } refusal)
        {
            return NotStarted(refusal);
        }

        string? sessionId = null;
        try
        {
            var project = string.IsNullOrWhiteSpace(projectId) ? null : projectId;
            var (id, _) = await _sessions.CreateScheduledTaskAsync(project, trustedFolders, cancellationToken).ConfigureAwait(false);
            sessionId = id;
            _ = await _sessions.OpenSessionAsync(RowFor(id), cancellationToken).ConfigureAwait(false);
            await _sessions.SendAsync(id, prompt, null, cancellationToken).ConfigureAwait(false);
            return new ScheduledSessionStart(true, id, null);
        }
        catch (InvalidOperationException exception) when (sessionId is null && exception is not ObjectDisposedException)
        {
            // Raised before any task was written. Each such sentence is written by the host and names no secret or path.
            return NotStarted(exception.Message);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var error = Describe(exception);
            if (sessionId is not null)
            {
                error = await CloseSafelyAsync(sessionId, error).ConfigureAwait(false);
            }

            return NotStarted(error);
        }
    }

    private static string? Unsupported(SessionApprovalMode approvalMode, string? model)
    {
        if (approvalMode == SessionApprovalMode.Skip)
        {
            return "Skip is not available for scheduled Cowork runs yet. The run was not started.";
        }

        return string.IsNullOrWhiteSpace(model) ? null : "Scheduled Cowork runs cannot choose a model yet. The run was not started.";
    }

    private async Task<string> CloseSafelyAsync(string sessionId, string error)
    {
        try
        {
            await _sessions.CloseSessionAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
            return error;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return $"{error} The partial session could not be closed ({exception.GetType().Name}).";
        }
    }

    private AgentRow RowFor(string sessionId)
    {
        var now = _clock.GetUtcNow();
        return new AgentRow
        {
            Summary = new SessionSummary { Id = sessionId, Title = RunTitle, Lifecycle = SessionLifecycle.Created, CreatedAt = now, LastActivity = now },
            Id = sessionId,
            Title = RunTitle,
            Status = AgentRowStatus.Ready,
            Group = AgentRowGroup.Idle,
            StatusText = "Ready",
            IconKey = "ready",
            LastActivityText = "now",
            IsLongRunning = false,
            IsStale = false,
            IsLost = false,
            AccessibleName = RunTitle,
        };
    }

    private static string Describe(Exception exception) => exception is OperationCanceledException
        ? "The scheduled Cowork session was cancelled before it started."
        : $"The scheduled Cowork session could not be started ({exception.GetType().Name}).";

    private static ScheduledSessionStart NotStarted(string error) => new(false, null, error);
}
