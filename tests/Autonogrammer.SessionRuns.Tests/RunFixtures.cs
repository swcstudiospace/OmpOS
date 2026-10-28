using Microsoft.Extensions.Logging.Abstractions;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sessions;
using Autonogrammer.Workflows;

namespace Autonogrammer.SessionRuns.Tests;

/// <summary>Shared inputs and helpers for the session-run tests.</summary>
internal static class RunFixtures
{
    /// <summary>The provision idempotency key the run tests use.</summary>
    public const string IdempotencyKey = "idem-1";

    /// <summary>The sandbox identifier <see cref="RecordingSandboxProvider"/> assigns to <see cref="IdempotencyKey"/>.</summary>
    public const string SandboxId = "sbx-idem-1";

    /// <summary>The approval timeout used when a test does not exercise the timeout.</summary>
    public static readonly TimeSpan DefaultApprovalTimeout = TimeSpan.FromSeconds(30);

    private const int MaxPolls = 1000;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>Builds the run input for one prompt on a session.</summary>
    /// <param name="sessionId">The owning session identifier.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <returns>The populated input.</returns>
    public static SessionRunInput InputFor(string sessionId, string prompt) => new()
    {
        RunId = "run-1",
        SessionId = sessionId,
        SandboxId = "sandbox-1",
        CorrelationId = $"{sessionId}/turn-1",
        Prompt = prompt,
        ResumeFromSeq = 0,
        IdempotencyKey = IdempotencyKey,
    };

    /// <summary>Creates an idle session and submits its first prompt, as the API does before it dispatches a run.</summary>
    /// <param name="sessions">The session service.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The running session's identifier.</returns>
    public static async Task<string> StartPromptAsync(SessionService sessions, string prompt, CancellationToken cancellationToken)
    {
        var session = await sessions.CreateAsync("owner-1", cancellationToken).ConfigureAwait(true);
        await sessions.SubmitPromptAsync(session.Id, prompt, cancellationToken).ConfigureAwait(true);
        return session.Id;
    }

    /// <summary>Builds the run activities over the given collaborators, with the system clock.</summary>
    /// <param name="sandbox">The sandbox provider.</param>
    /// <param name="harness">The harness session factory.</param>
    /// <param name="repository">The session store.</param>
    /// <param name="sessions">The session service over the same store.</param>
    /// <param name="observer">The run observer.</param>
    /// <param name="approvalTimeout">How long a permission request waits for its decision; the default when null.</param>
    /// <returns>The run activities.</returns>
    public static SessionRunActivities Activities(
        ISandboxProvider sandbox,
        IHarnessSessionFactory harness,
        ISessionRepository repository,
        SessionService sessions,
        ISessionRunObserver observer,
        TimeSpan? approvalTimeout = null) =>
        new(
            sandbox,
            harness,
            repository,
            sessions,
            TimeProvider.System,
            observer,
            NullLogger<SessionRunActivities>.Instance,
            approvalTimeout ?? DefaultApprovalTimeout);

    /// <summary>Polls the store until a session reaches the given status.</summary>
    /// <param name="repository">The session store.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="status">The status to wait for.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <exception cref="TimeoutException">The session did not reach the status within the poll budget.</exception>
    public static async Task WaitForStatusAsync(ISessionRepository repository, string sessionId, SessionStatus status, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxPolls; attempt++)
        {
            var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(true);
            if (session?.Status == status)
            {
                return;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(true);
        }

        throw new TimeoutException($"Session '{sessionId}' did not reach {status}.");
    }
}
