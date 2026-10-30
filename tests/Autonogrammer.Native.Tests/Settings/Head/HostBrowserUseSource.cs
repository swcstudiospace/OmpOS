using Autonogrammer.Presentation.Settings.BrowserUse;

namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>A Browser source that never acquires, for the settings host order test.</summary>
internal sealed class HostBrowserUseSource : IBrowserUseSettingsSource
{
    /// <inheritdoc />
    public Task<BrowserUseSnapshot> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new BrowserUseSnapshot(BrowserUseChromiumState.BrowserNotInstalled, null, BrowserUseSettingsValues.Defaults, null));

    /// <inheritdoc />
    public Task SaveAsync(BrowserUseSettingsValues values, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task AcquireAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
