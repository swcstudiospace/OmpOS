using System.Globalization;
using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.BrowserUse;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// Adapts <see cref="BrowserUseOptions"/>, <see cref="ChromiumLocator"/> and <see cref="ChromiumAcquirer"/> to the Presentation seam.
/// <see cref="AcquireAsync"/> always passes user confirmation, and the page calls it only from the dialog.
/// </summary>
internal sealed class BrowserUseSettingsSource : IBrowserUseSettingsSource
{
    private readonly BrowserUseOptions _options;
    private readonly ChromiumLocator _locator;
    private readonly ChromiumManifest _manifest;
    private readonly ChromiumAcquirer _acquirer;
    private readonly string _stateDirectory;
    private readonly string _platformId;
    private readonly ChromiumOsFamily _osFamily;
    private readonly FileSettingsStore? _store;

    /// <summary>Initializes the adapter.</summary>
    /// <param name="options">The live options object the head binds.</param>
    /// <param name="locator">The locator.</param>
    /// <param name="manifest">The embedded manifest.</param>
    /// <param name="acquirer">The acquirer. Called only after the dialog confirms.</param>
    /// <param name="stateDirectory">The head state directory.</param>
    /// <param name="platformId">The manifest platform id.</param>
    /// <param name="osFamily">The OS family used to locate a system binary.</param>
    /// <param name="store">The settings file the values persist to. Null keeps saves in memory (tests).</param>
    public BrowserUseSettingsSource(
        BrowserUseOptions options,
        ChromiumLocator locator,
        ChromiumManifest manifest,
        ChromiumAcquirer acquirer,
        string stateDirectory,
        string platformId,
        ChromiumOsFamily osFamily,
        FileSettingsStore? store = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(acquirer);
        ArgumentException.ThrowIfNullOrEmpty(stateDirectory);
        ArgumentException.ThrowIfNullOrEmpty(platformId);
        _options = options;
        _locator = locator;
        _manifest = manifest;
        _acquirer = acquirer;
        _stateDirectory = stateDirectory;
        _platformId = platformId;
        _osFamily = osFamily;
        _store = store;
    }

    /// <inheritdoc />
    public Task<BrowserUseSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var location = _locator.Locate(_options, _stateDirectory, _osFamily);
        var row = _manifest.RowFor(_platformId);
        var state = location.State switch
        {
            BrowserLocateState.Found => BrowserUseChromiumState.Ready,
            BrowserLocateState.BrowserTooOld => BrowserUseChromiumState.BrowserTooOld,
            BrowserLocateState.BrowserNotInstalled when row is null => BrowserUseChromiumState.ManagedBrowserNotPinned,
            _ => BrowserUseChromiumState.BrowserNotInstalled,
        };
        BrowserUseManifestOffer? offer = row is null
            ? null
            : new BrowserUseManifestOffer(row.Version, HostOf(row.Source), row.SizeBytes, BrowserUseCopy.LicenceLine);
        var version = location.MajorVersion?.ToString(CultureInfo.InvariantCulture);
        return Task.FromResult(new BrowserUseSnapshot(state, version, ReadValues(), offer));
    }

    /// <inheritdoc />
    public Task SaveAsync(BrowserUseSettingsValues values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        cancellationToken.ThrowIfCancellationRequested();

        // Persist first: a failed write changes neither the file nor the live options.
        _store?.SetBrowserUse(values);
        BrowserUseSettingsPersistence.Apply(values, _options);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task AcquireAsync(CancellationToken cancellationToken)
    {
        var result = await _acquirer.AcquireAsync(_manifest, _platformId, _stateDirectory, userConfirmed: true, cancellationToken).ConfigureAwait(false);
        if (result.Outcome != AcquireOutcome.Installed)
        {
            throw new SettingsSourceException("Cowork:BrowserUse acquisition was refused.");
        }
    }

    private BrowserUseSettingsValues ReadValues() => BrowserUseSettingsPersistence.Read(_options);

    private static string HostOf(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            return uri.Host;
        }

        return source;
    }
}
