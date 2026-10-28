using Microsoft.Extensions.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services.WorkspaceSettings;
using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Harness.Automations.Hosted;

namespace Autonogrammer.Api.Automations;

/// <summary>
/// Fills the settings seam <see cref="IWorkspaceAutomationsProvider"/> with the caller's automations, so the existing
/// <c>GET /automations</c> answer stops saying that no engine is configured. It lists the caller's own automations only, found through
/// the request's <see cref="IUserContext"/>, and answers null (not configured) when automations are off or no request is in scope.
/// </summary>
internal sealed class HostedAutomationsSettingsProvider : IWorkspaceAutomationsProvider
{
    private readonly IOptions<AutomationOptions> _options;
    private readonly IHttpContextAccessor _context;
    private readonly AutomationService _service;
    private readonly IAutomationStore _store;

    /// <summary>Initializes a new instance of the <see cref="HostedAutomationsSettingsProvider"/> class.</summary>
    /// <param name="options">The automation options.</param>
    /// <param name="context">The accessor of the current request.</param>
    /// <param name="service">The automation service.</param>
    /// <param name="store">The automation store, for the run history.</param>
    public HostedAutomationsSettingsProvider(
        IOptions<AutomationOptions> options,
        IHttpContextAccessor context,
        AutomationService service,
        IAutomationStore store)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(store);
        _options = options;
        _context = context;
        _service = service;
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkspaceAutomationInfo>?> ListAsync(CancellationToken cancellationToken)
    {
        var http = _context.HttpContext;
        if (!_options.Value.Enabled || http is null)
        {
            return null;
        }

        var owner = http.RequestServices.GetRequiredService<IUserContext>().Name;
        var result = new List<WorkspaceAutomationInfo>();
        foreach (var automation in await _service.ListAsync(owner, cancellationToken).ConfigureAwait(false))
        {
            var history = await _store.GetHistoryAsync(automation.Id, cancellationToken).ConfigureAwait(false);
            var last = history.FirstOrDefault(static entry => entry.Status != AutomationRunStatus.Running);
            result.Add(new WorkspaceAutomationInfo
            {
                Id = automation.Id,
                Trigger = Describe(automation),
                Enabled = automation.Enabled,
                MaxRuntimeSeconds = TimeSpan.FromMinutes(automation.MaxRuntimeMinutes).TotalSeconds,
                LastRun = last is null
                    ? null
                    : new AutomationLastRunInfo { Outcome = Outcome(last.Status), Reason = last.Error ?? string.Empty, RecordedAt = last.FinishedAt ?? last.StartedAt },
            });
        }

        return result;
    }

    private static string Describe(UserAutomation automation)
    {
        var parts = automation.Triggers.Select(static trigger => trigger.Kind == AutomationTriggerKind.Cron
            ? "cron " + trigger.Cron
            : trigger.Flavour == WebhookFlavour.GitHub ? "GitHub webhook" : "webhook");
        return string.Join("; ", parts);
    }

    private static string Outcome(AutomationRunStatus status) => status switch
    {
        AutomationRunStatus.Succeeded or AutomationRunStatus.Launched => "succeeded",
        AutomationRunStatus.TimedOut => "timed-out",
        AutomationRunStatus.Cancelled => "cancelled",
        AutomationRunStatus.Skipped => "refused",
        _ => "failed",
    };
}
