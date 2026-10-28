using Autonogrammer.Presentation.Automations;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The manager of the Automations page with nothing saved: the page opens over it and shows its empty state.</summary>
internal sealed class ScriptedAutomationsManager : IAutomationsManager
{
    /// <summary>Gets or sets the automations it lists.</summary>
    public List<HostedAutomationEntry> Entries { get; set; } = [];

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedTargetKinds => ["local"];

    /// <inheritdoc/>
    public bool SupportsWebhooks => false;

    /// <inheritdoc/>
    public string PageNote => AutomationsCopy.DesktopNote;

    /// <inheritdoc/>
    public Task<IReadOnlyList<HostedAutomationEntry>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<HostedAutomationEntry>>([.. Entries]);

    /// <inheritdoc/>
    public Task<AutomationSaveResult> SaveAsync(string? id, AutomationDraft draft, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<HostedRunEntry> RunNowAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<IReadOnlyList<HostedRunEntry>> HistoryAsync(string id, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<HostedRunEntry>>([]);

    /// <inheritdoc/>
    public Task<AutomationCronPreview> PreviewCronAsync(string expression, string timeZone, int count, CancellationToken cancellationToken) =>
        Task.FromResult(new AutomationCronPreview([], string.Empty));

    /// <inheritdoc/>
    public Task<string> RotateWebhookSecretAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
}
