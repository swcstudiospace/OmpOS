using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Presentation.Automations;
using Autonogrammer.Workspace;

namespace Autonogrammer.Native.Composition.Automations;

/// <summary>
/// Runs an automation as a new agent session of this head: the session is created like any other (it shows in the rail, with the run mode of the user), the first run of each automation
/// asks the user to confirm through the approval card of that session, the prompt is sent as its first turn and the run ends when the turn ends. The run history records the session identifier.
/// </summary>
internal sealed class LocalSessionAutomationTarget : IAutomationTarget
{
    private readonly Func<IAutomationSessions?> _sessions;
    private readonly Func<string> _workspaceRoot;
    private readonly ISetupConsentStore _consent;

    /// <summary>Initializes a new instance of the <see cref="LocalSessionAutomationTarget"/> class.</summary>
    /// <param name="sessions">Returns the sessions of the head, or null while the head has not opened them yet.</param>
    /// <param name="workspaceRoot">Returns the workspace root the runs work in.</param>
    /// <param name="consent">The record of the automations the user allowed to run.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public LocalSessionAutomationTarget(Func<IAutomationSessions?> sessions, Func<string> workspaceRoot, ISetupConsentStore consent)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        ArgumentNullException.ThrowIfNull(consent);
        _sessions = sessions;
        _workspaceRoot = workspaceRoot;
        _consent = consent;
    }

    /// <inheritdoc/>
    public AutomationTargetKind Kind => AutomationTargetKind.Local;

    /// <summary>
    /// Returns the key under which the consent to an automation is recorded: its identifier, with a hash of the prompt and the workspace, so editing the prompt asks again.
    /// </summary>
    /// <param name="automation">The automation.</param>
    /// <param name="workspaceRoot">The workspace root of the run.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static SetupConsentKey ConsentKey(UserAutomation automation, string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(automation);
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(automation.Prompt + "\n" + workspaceRoot));
        return new SetupConsentKey("automation:" + automation.Id, Convert.ToHexStringLower(hash));
    }

    /// <inheritdoc/>
    public async Task<AutomationTargetResult> RunAsync(AutomationRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_sessions() is not { } sessions)
        {
            return AutomationTargetResult.Failed(AutomationsCopy.SessionsNotReady);
        }

        var automation = request.Automation;
        var workspaceRoot = _workspaceRoot();
        var session = await sessions.CreateAsync(automation.Name, workspaceRoot, cancellationToken).ConfigureAwait(false);
        try
        {
            var key = ConsentKey(automation, workspaceRoot);
            if (!await _consent.IsGrantedAsync(key, cancellationToken).ConfigureAwait(false))
            {
                var allowed = await session.ConfirmAsync(
                    new AutomationConfirmation(AutomationsCopy.ConsentCardTitle, AutomationsCopy.ConsentSummary(automation.Name, workspaceRoot)),
                    cancellationToken).ConfigureAwait(false);
                if (!allowed)
                {
                    await session.CloseAsync().ConfigureAwait(false);
                    return new AutomationTargetResult { Status = AutomationRunStatus.Failed, ExternalId = session.Id, Error = AutomationsCopy.NotConfirmed };
                }

                await _consent.RecordGrantAsync(key, cancellationToken).ConfigureAwait(false);
            }

            var turn = await session.RunAsync(request.Prompt, cancellationToken).ConfigureAwait(false);
            return turn.Completed
                ? AutomationTargetResult.Succeeded(session.Id)
                : new AutomationTargetResult { Status = AutomationRunStatus.Failed, ExternalId = session.Id, Error = turn.Error ?? AutomationsCopy.TurnFailed };
        }
        catch (OperationCanceledException)
        {
            // The run timed out or the head is closing: the session is stopped so no agent keeps working after its run ended.
            await session.CloseAsync().ConfigureAwait(false);
            throw;
        }
    }
}
