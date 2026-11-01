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

    /// <summary>Initializes the adapter.</summary>
    /// <param name="options">The live options object the head binds.</param>
    /// <param name="locator">The locator.</param>
    /// <param name="manifest">The embedded manifest.</param>
    /// <param name="acquirer">The acquirer. Called only after the dialog confirms.</param>
    /// <param name="stateDirectory">The head state directory.</param>
    /// <param name="platformId">The manifest platform id.</param>
    /// <param name="osFamily">The OS family used to locate a system binary.</param>
    public BrowserUseSettingsSource(
        BrowserUseOptions options,
        ChromiumLocator locator,
        ChromiumManifest manifest,
        ChromiumAcquirer acquirer,
        string stateDirectory,
        string platformId,
        ChromiumOsFamily osFamily)
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
        _options.ExecutablePath = values.ExecutablePath;
        _options.PolicyMode = Enum.Parse<PolicyMode>(values.PolicyMode.ToString());
        _options.DownloadMode = Enum.Parse<DownloadMode>(values.DownloadMode.ToString());
        _options.SandboxMode = Enum.Parse<SandboxMode>(values.SandboxMode.ToString());
        _options.PersistentProfile = values.PersistentProfile;
        _options.AllowDispatchSessions = values.AllowDispatchSessions;
        _options.LocalOriginsTrusted = values.LocalOriginsTrusted;
        Replace(_options.AllowedHosts, values.AllowedHosts);
        Replace(_options.DeniedHosts, values.DeniedHosts);
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

    private BrowserUseSettingsValues ReadValues() => new(
        _options.ExecutablePath,
        Enum.Parse<BrowserUsePolicyMode>(_options.PolicyMode.ToString()),
        _options.AllowedHosts.ToArray(),
        _options.DeniedHosts.ToArray(),
        Enum.Parse<BrowserUseDownloadMode>(_options.DownloadMode.ToString()),
        Enum.Parse<BrowserUseSandboxMode>(_options.SandboxMode.ToString()),
        _options.PersistentProfile,
        _options.AllowDispatchSessions,
        _options.LocalOriginsTrusted);

    private static void Replace(IList<string> target, IReadOnlyList<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static string HostOf(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            return uri.Host;
        }

        return source;
    }
}
