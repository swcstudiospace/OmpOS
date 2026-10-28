namespace OmpOS.Presentation.Automations;

/// <summary>
/// The user-defined automations a <see cref="HostedAutomationsViewModel"/> lists and edits. The presentation layer may not reference the
/// automation harness, so a head adapts its API or harness to this interface. Expected failures are thrown as
/// <see cref="AutomationsSourceException"/>; <see cref="ListAsync"/> throws <see cref="AutomationsUnavailableException"/> when the
/// service has automations switched off. Any other exception is a defect.
/// </summary>
public interface IAutomationsManager
{
    /// <summary>Gets the target kinds this head can run: <c>cloud</c> on the web head, <c>local</c> and <c>cloud</c> on the desktop.</summary>
    IReadOnlyList<string> SupportedTargetKinds { get; }

    /// <summary>
    /// Gets whether this head can receive the webhook of an automation: true when the service that hosts the automations has a public address (the API),
    /// false on the desktop, where an automation starts from its schedule only. The editor offers the webhook trigger only when this is true.
    /// </summary>
    bool SupportsWebhooks { get; }

    /// <summary>Gets a sentence the page shows under its description (where the automations run and what they need), or an empty string.</summary>
    string PageNote { get; }

    /// <summary>Lists every automation.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The automations.</returns>
    /// <exception cref="AutomationsUnavailableException">The service has automations switched off.</exception>
    /// <exception cref="AutomationsSourceException">The automations could not be read.</exception>
    Task<IReadOnlyList<HostedAutomationEntry>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates or updates an automation.</summary>
    /// <param name="id">The automation to update, or null to create one.</param>
    /// <param name="draft">The settings to save.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The saved entry and, when saving generated a webhook secret, that secret.</returns>
    /// <exception cref="AutomationsSourceException">The automation could not be saved.</exception>
    Task<AutomationSaveResult> SaveAsync(string? id, AutomationDraft draft, CancellationToken cancellationToken);

    /// <summary>Turns an automation on or off.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="enabled">True to turn the automation on.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is written.</returns>
    /// <exception cref="AutomationsSourceException">The change could not be written.</exception>
    Task SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken);

    /// <summary>Deletes an automation.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the automation is deleted.</returns>
    /// <exception cref="AutomationsSourceException">The automation could not be deleted.</exception>
    Task DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>Starts a run of an automation now.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The run that was started.</returns>
    /// <exception cref="AutomationsSourceException">The run could not be started.</exception>
    Task<HostedRunEntry> RunNowAsync(string id, CancellationToken cancellationToken);

    /// <summary>Lists the runs of an automation.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs, newest first.</returns>
    /// <exception cref="AutomationsSourceException">The history could not be read.</exception>
    Task<IReadOnlyList<HostedRunEntry>> HistoryAsync(string id, CancellationToken cancellationToken);

    /// <summary>Computes the next runs of a cron expression.</summary>
    /// <param name="expression">The cron expression.</param>
    /// <param name="timeZone">The IANA time zone the expression is read in.</param>
    /// <param name="count">How many runs to compute.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The next runs, or the reason the expression is invalid.</returns>
    /// <exception cref="AutomationsSourceException">The preview could not be computed.</exception>
    Task<AutomationCronPreview> PreviewCronAsync(string expression, string timeZone, int count, CancellationToken cancellationToken);

    /// <summary>Replaces the webhook secret of an automation.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new secret. It is shown once and cannot be read again.</returns>
    /// <exception cref="AutomationsSourceException">The secret could not be replaced.</exception>
    Task<string> RotateWebhookSecretAsync(string id, CancellationToken cancellationToken);
}
