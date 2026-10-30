namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Creates, changes, lists and deletes the automations of one owner, validating each change and keeping the webhook secret in step
/// with the triggers. An automation of another owner reads as absent. No method returns a stored secret; only a call that generates one
/// returns it, once, in <see cref="AutomationChange.WebhookSecret"/>.
/// </summary>
public sealed class AutomationService
{
    private readonly IAutomationStore _store;
    private readonly IAutomationSecretStore _secrets;
    private readonly TimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="AutomationService"/> class.</summary>
    /// <param name="store">The automation store.</param>
    /// <param name="secrets">The secret store for webhook signing secrets.</param>
    /// <param name="clock">The clock.</param>
    public AutomationService(IAutomationStore store, IAutomationSecretStore secrets, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _secrets = secrets;
        _clock = clock;
    }

    /// <summary>Lists an owner's automations, newest first.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The automations.</returns>
    public async Task<IReadOnlyList<UserAutomation>> ListAsync(string owner, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        var all = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(automation => string.Equals(automation.Owner, owner, StringComparison.Ordinal)).OrderByDescending(static automation => automation.CreatedAt).ToArray();
    }

    /// <summary>Reads one of an owner's automations.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The automation, or null when it does not exist or belongs to another owner.</returns>
    public async Task<UserAutomation?> GetAsync(string owner, string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var automation = await _store.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return automation is not null && string.Equals(automation.Owner, owner, StringComparison.Ordinal) ? automation : null;
    }

    /// <summary>Tells whether an automation has a webhook secret stored, without reading the value out.</summary>
    /// <param name="automation">The automation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a secret is stored.</returns>
    public async Task<bool> HasWebhookSecretAsync(UserAutomation automation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(automation);
        return await _secrets.GetAsync(AutomationSecrets.WebhookReference(automation.Id), cancellationToken).ConfigureAwait(false) is not null;
    }

    /// <summary>Creates an automation.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="draft">The fields.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The result. A webhook secret is generated when the draft has a webhook trigger.</returns>
    public async Task<AutomationChange> CreateAsync(string owner, AutomationDraft draft, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(draft);
        var now = _clock.GetUtcNow();
        var automation = Build(draft, WebhookSecretGenerator.GenerateAutomationId(), owner, now, now);
        var issues = AutomationValidator.Validate(automation).ToList();
        var owned = (await ListAsync(owner, cancellationToken).ConfigureAwait(false)).Count;
        if (owned >= AutomationLimits.MaxAutomationsPerOwner)
        {
            issues.Add(new AutomationValidationIssue("limits", "An owner may have at most 50 automations."));
        }

        if (issues.Count > 0)
        {
            return AutomationChange.Invalid(issues);
        }

        string? secret = null;
        if (automation.HasWebhook)
        {
            secret = WebhookSecretGenerator.Generate();
            await _secrets.SetAsync(AutomationSecrets.WebhookReference(automation.Id), secret, cancellationToken).ConfigureAwait(false);
        }

        await _store.SaveAsync(automation, cancellationToken).ConfigureAwait(false);
        return AutomationChange.Applied(automation, secret);
    }

    /// <summary>Replaces an automation's definition. The identifier, owner and creation time stay.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="draft">The new fields.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The result. A secret is generated when the new definition gains its first webhook trigger, and deleted when it loses its last.</returns>
    public async Task<AutomationChange> ReplaceAsync(string owner, string id, AutomationDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var existing = await GetAsync(owner, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return AutomationChange.Missing();
        }

        var automation = Build(draft, existing.Id, existing.Owner, existing.CreatedAt, _clock.GetUtcNow());
        var issues = AutomationValidator.Validate(automation);
        if (issues.Count > 0)
        {
            return AutomationChange.Invalid(issues);
        }

        var reference = AutomationSecrets.WebhookReference(automation.Id);
        string? secret = null;
        if (automation.HasWebhook && await _secrets.GetAsync(reference, cancellationToken).ConfigureAwait(false) is null)
        {
            secret = WebhookSecretGenerator.Generate();
            await _secrets.SetAsync(reference, secret, cancellationToken).ConfigureAwait(false);
        }
        else if (!automation.HasWebhook)
        {
            await _secrets.DeleteAsync(reference, cancellationToken).ConfigureAwait(false);
        }

        await _store.SaveAsync(automation, cancellationToken).ConfigureAwait(false);
        return AutomationChange.Applied(automation, secret);
    }

    /// <summary>Turns an automation's triggers on or off.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="enabled">True to enable.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The result.</returns>
    public async Task<AutomationChange> SetEnabledAsync(string owner, string id, bool enabled, CancellationToken cancellationToken)
    {
        var existing = await GetAsync(owner, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return AutomationChange.Missing();
        }

        var changed = existing with { Enabled = enabled, UpdatedAt = _clock.GetUtcNow() };
        await _store.SaveAsync(changed, cancellationToken).ConfigureAwait(false);
        return AutomationChange.Applied(changed, null);
    }

    /// <summary>Replaces an automation's webhook secret with a new one. The old secret stops working at once.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The result with the new secret, or an invalid result when the automation has no webhook trigger.</returns>
    public async Task<AutomationChange> RotateWebhookSecretAsync(string owner, string id, CancellationToken cancellationToken)
    {
        var existing = await GetAsync(owner, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return AutomationChange.Missing();
        }

        if (!existing.HasWebhook)
        {
            return AutomationChange.Invalid([new AutomationValidationIssue("webhook", "The automation has no webhook trigger.")]);
        }

        var secret = WebhookSecretGenerator.Generate();
        await _secrets.SetAsync(AutomationSecrets.WebhookReference(existing.Id), secret, cancellationToken).ConfigureAwait(false);
        return AutomationChange.Applied(existing, secret);
    }

    /// <summary>Deletes an automation with its history and its webhook secret.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the automation existed and was deleted.</returns>
    public async Task<bool> DeleteAsync(string owner, string id, CancellationToken cancellationToken)
    {
        var existing = await GetAsync(owner, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        await _secrets.DeleteAsync(AutomationSecrets.WebhookReference(existing.Id), cancellationToken).ConfigureAwait(false);
        return await _store.DeleteAsync(existing.Id, cancellationToken).ConfigureAwait(false);
    }

    private static UserAutomation Build(AutomationDraft draft, string id, string owner, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new()
        {
            Id = id,
            Owner = owner,
            Name = draft.Name.Trim(),
            Prompt = draft.Prompt,
            Enabled = draft.Enabled,
            Triggers = draft.Triggers,
            Target = draft.Target,
            AllowedTools = draft.AllowedTools ?? AutomationTools.Default,
            MaxConcurrentRuns = draft.MaxConcurrentRuns,
            MaxRunsPerHour = draft.MaxRunsPerHour,
            TimeZone = draft.TimeZone,
            MaxRuntimeMinutes = draft.MaxRuntimeMinutes,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
}
