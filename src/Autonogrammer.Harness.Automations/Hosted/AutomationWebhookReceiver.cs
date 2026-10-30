namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// The logic behind the public webhook URL: it finds the automation, reads its secret, lets <see cref="WebhookTrigger"/> judge the
/// delivery and, for an authentic matching one, asks the <see cref="AutomationRunner"/> for a run. It has no HTTP in it, so the same
/// logic serves the API receiver and any other host.
/// </summary>
public sealed class AutomationWebhookReceiver
{
    private readonly IAutomationStore _store;
    private readonly IAutomationSecretStore _secrets;
    private readonly WebhookTrigger _trigger;
    private readonly AutomationRunner _runner;

    /// <summary>Initializes a new instance of the <see cref="AutomationWebhookReceiver"/> class.</summary>
    /// <param name="store">The automation store.</param>
    /// <param name="secrets">The secret store.</param>
    /// <param name="trigger">The trigger that judges deliveries.</param>
    /// <param name="runner">The runner that starts runs.</param>
    public AutomationWebhookReceiver(IAutomationStore store, IAutomationSecretStore secrets, WebhookTrigger trigger, AutomationRunner runner)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(runner);
        _store = store;
        _secrets = secrets;
        _trigger = trigger;
        _runner = runner;
    }

    /// <summary>Gets the largest body accepted, in bytes.</summary>
    public int MaxBodyBytes => _trigger.MaxBodyBytes;

    /// <summary>Handles one delivery.</summary>
    /// <param name="automationId">The identifier from the URL.</param>
    /// <param name="request">The delivery.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The receipt.</returns>
    public async Task<WebhookReceipt> HandleAsync(string automationId, WebhookRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentNullException.ThrowIfNull(request);
        var automation = await _store.GetAsync(automationId, cancellationToken).ConfigureAwait(false);
        var secret = automation is null
            ? null
            : await _secrets.GetAsync(AutomationSecrets.WebhookReference(automation.Id), cancellationToken).ConfigureAwait(false);
        var decision = _trigger.Evaluate(automation, secret, request);
        if (decision.Outcome != WebhookOutcome.Accepted)
        {
            return new WebhookReceipt(decision.Outcome, decision.Reason, null, null);
        }

        var start = await _runner.StartAsync(automation!, decision.Context!, cancellationToken).ConfigureAwait(false);
        return new WebhookReceipt(WebhookOutcome.Accepted, start.Admitted ? "accepted" : "refused", start.RunId, start.Refusal);
    }
}
