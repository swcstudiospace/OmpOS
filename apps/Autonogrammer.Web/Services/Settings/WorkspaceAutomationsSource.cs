using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Presentation.Automations;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>
/// The Automations page over the API: a read-only list of the automations the API host configured. An API with no automation engine is the page's
/// error state with the API's own reason. Enabling and resuming are refused: a web head never changes or starts an automation.
/// </summary>
internal sealed class WorkspaceAutomationsSource : IAutomationsSource
{
    /// <summary>The sentence a refused change shows.</summary>
    internal const string ReadOnlyMessage = "Automations are read-only in the web app. Change them in the desktop app.";

    private readonly WorkspaceSettingsClient _client;
    private IReadOnlyList<AutomationEntry> _snapshot = [];

    /// <summary>Initializes a new instance of the <see cref="WorkspaceAutomationsSource"/> class.</summary>
    /// <param name="client">The settings client.</param>
    internal WorkspaceAutomationsSource(WorkspaceSettingsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc/>
    /// <exception cref="AutomationsSourceException">The automations cannot be read; the message says why.</exception>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        AutomationsResponse response;
        try
        {
            response = await _client.GetAutomationsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (WorkspaceSettingsFailure.IsExpected(exception))
        {
            throw new AutomationsSourceException(WorkspaceSettingsFailure.Describe(exception), exception);
        }

        if (!response.Available)
        {
            throw new AutomationsSourceException(response.Reason ?? "No automation engine is configured on this API.");
        }

        _snapshot = [.. response.Automations.Select(ToEntry)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<AutomationEntry> Snapshot() => _snapshot;

    /// <inheritdoc/>
    public Task SetEnabledAsync(string automationId, bool enabled, CancellationToken cancellationToken) =>
        Task.FromException(new AutomationsSourceException(ReadOnlyMessage));

    /// <inheritdoc/>
    public Task ResumeAsync(string automationId, CancellationToken cancellationToken) =>
        Task.FromException(new AutomationsSourceException(ReadOnlyMessage));

    private static AutomationEntry ToEntry(WorkspaceAutomationInfo automation) => new(
        automation.Id,
        automation.Trigger,
        automation.Enabled,
        TimeSpan.FromSeconds(automation.MaxRuntimeSeconds),
        automation.LastRun is null ? null : new AutomationLastRun(ToOutcome(automation.LastRun.Outcome), automation.LastRun.Reason, automation.LastRun.RecordedAt),
        automation.Pause is null ? null : new AutomationPauseEntry(automation.Pause.Guard, automation.Pause.Reason, automation.Pause.HowToResume));

    private static AutomationOutcome ToOutcome(string outcome) => outcome switch
    {
        "succeeded" => AutomationOutcome.Succeeded,
        "failed" => AutomationOutcome.Failed,
        "timed-out" => AutomationOutcome.TimedOut,
        "cancelled" => AutomationOutcome.Cancelled,
        "blocked" => AutomationOutcome.Blocked,
        "refused" => AutomationOutcome.Refused,
        _ => throw new AutomationsSourceException("The server reported a run outcome this page does not know."),
    };
}
