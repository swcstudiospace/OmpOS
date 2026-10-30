namespace Autonogrammer.Presentation.Settings.BrowserUse;


/// <summary>
/// The Browser settings page: Chromium state, the options draft and the acquisition dialog.
/// The dialog's confirm path is the only caller of <see cref="IBrowserUseSettingsSource.AcquireAsync"/>.
/// </summary>
public sealed class BrowserUseSettingsPage : EditableSettingsPage<BrowserUseSettingsValues>
{
    private readonly IBrowserUseSettingsSource _source;
    private BrowserUseChromiumState _chromiumState = BrowserUseChromiumState.BrowserNotInstalled;
    private string _version = string.Empty;
    private BrowserUseManifestOffer? _offer;
    private BrowserUseAcquisitionDialog? _dialog;

    /// <summary>Initializes a new instance of the <see cref="BrowserUseSettingsPage"/> class.</summary>
    /// <param name="source">Where Chromium state, options and acquisition live.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public BrowserUseSettingsPage(IBrowserUseSettingsSource source)
        : base(SettingsSection.BrowserUse, BrowserUseCopy.Title, BrowserUseSettingsValues.Defaults)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the Chromium state from the last load.</summary>
    public BrowserUseChromiumState ChromiumState => _chromiumState;

    /// <summary>Gets the ready binary's major version, or an empty string.</summary>
    public string Version => _version;

    /// <summary>Gets the one sentence for the current Chromium state.</summary>
    public string ChromiumSentence => _chromiumState switch
    {
        BrowserUseChromiumState.Ready => BrowserUseCopy.ReadySentence(_version),
        BrowserUseChromiumState.BrowserTooOld => BrowserUseCopy.BrowserTooOldSentence,
        BrowserUseChromiumState.ManagedBrowserNotPinned => BrowserUseCopy.ManagedBrowserNotPinnedSentence,
        _ => BrowserUseCopy.BrowserNotInstalledSentence,
    };

    /// <summary>Gets the one action for the current Chromium state.</summary>
    public string FixAction => _chromiumState switch
    {
        BrowserUseChromiumState.Ready => BrowserUseCopy.ReadyAction,
        BrowserUseChromiumState.BrowserTooOld => BrowserUseCopy.BrowserTooOldAction,
        BrowserUseChromiumState.ManagedBrowserNotPinned => BrowserUseCopy.ManagedBrowserNotPinnedAction,
        _ => BrowserUseCopy.BrowserNotInstalledAction,
    };

    /// <summary>Gets a value indicating whether a manifest row exists, so the install action may be shown.</summary>
    public bool IsAcquisitionVisible => _offer is not null;

    /// <summary>Gets the open acquisition dialog, or null.</summary>
    public BrowserUseAcquisitionDialog? Dialog => _dialog;

    /// <summary>Gets a value indicating whether the acquisition dialog is open.</summary>
    public bool HasAcquisitionDialog => _dialog is not null;

    /// <summary>Gets the risk line for origin policy Open, or an empty string.</summary>
    public string PolicyRiskText => Draft.PolicyMode == BrowserUsePolicyMode.Open ? BrowserUseCopy.OpenRisk : string.Empty;

    /// <summary>Gets the risk line for trusted local origins, or an empty string.</summary>
    public string LocalOriginsRiskText => Draft.LocalOriginsTrusted ? BrowserUseCopy.LocalOriginsRisk : string.Empty;

    /// <summary>Gets the open dialog sentence, or an empty string.</summary>
    public string DialogText => _dialog?.Text ?? string.Empty;

    /// <summary>Gets or sets the draft executable path. Ignored while the page is not ready.</summary>
    public string ExecutablePath
    {
        get => Draft.ExecutablePath;
        set => SetDraft(Draft with { ExecutablePath = value ?? string.Empty });
    }

    /// <summary>Gets or sets the draft origin policy. Ignored while the page is not ready.</summary>
    public BrowserUsePolicyMode PolicyMode
    {
        get => Draft.PolicyMode;
        set => SetDraft(Draft with { PolicyMode = value });
    }

    /// <summary>Gets or sets the draft allowed hosts, one host per line.</summary>
    public string AllowedHostsText
    {
        get => string.Join('\n', Draft.AllowedHosts);
        set => SetDraft(Draft with { AllowedHosts = SplitHosts(value) });
    }

    /// <summary>Gets or sets the draft denied hosts, one host per line.</summary>
    public string DeniedHostsText
    {
        get => string.Join('\n', Draft.DeniedHosts);
        set => SetDraft(Draft with { DeniedHosts = SplitHosts(value) });
    }

    /// <summary>Gets or sets the draft download policy.</summary>
    public BrowserUseDownloadMode DownloadMode
    {
        get => Draft.DownloadMode;
        set => SetDraft(Draft with { DownloadMode = value });
    }

    /// <summary>Gets or sets the draft sandbox policy.</summary>
    public BrowserUseSandboxMode SandboxMode
    {
        get => Draft.SandboxMode;
        set => SetDraft(Draft with { SandboxMode = value });
    }

    /// <summary>Gets or sets whether the draft keeps a named profile.</summary>
    public bool PersistentProfile
    {
        get => Draft.PersistentProfile;
        set => SetDraft(Draft with { PersistentProfile = value });
    }

    /// <summary>Gets or sets whether the draft allows dispatch sessions.</summary>
    public bool AllowDispatchSessions
    {
        get => Draft.AllowDispatchSessions;
        set => SetDraft(Draft with { AllowDispatchSessions = value });
    }

    /// <summary>Gets or sets whether the draft treats granted local origins as trusted.</summary>
    public bool LocalOriginsTrusted
    {
        get => Draft.LocalOriginsTrusted;
        set => SetDraft(Draft with { LocalOriginsTrusted = value });
    }

    /// <summary>Opens the acquisition dialog when a manifest row exists. Does not acquire.</summary>
    /// <returns>The dialog, or null when acquisition is hidden.</returns>
    public BrowserUseAcquisitionDialog? OpenAcquisition()
    {
        if (_offer is null || State != SettingsPageState.Ready)
        {
            return null;
        }

        _dialog = new BrowserUseAcquisitionDialog(_offer, _source.AcquireAsync);
        NotifyDialog();
        return _dialog;
    }

    /// <summary>Dismisses the acquisition dialog without acquiring.</summary>
    public void DismissAcquisition()
    {
        _dialog?.Dismiss();
        _dialog = null;
        NotifyDialog();
    }

    /// <inheritdoc/>
    protected override async Task<BrowserUseSettingsValues> LoadValuesAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        _chromiumState = snapshot.State;
        _version = snapshot.Version ?? string.Empty;
        _offer = snapshot.Offer;
        _dialog = null;
        return snapshot.Values;
    }

    /// <inheritdoc/>
    protected override Task SaveValuesAsync(BrowserUseSettingsValues values, CancellationToken cancellationToken) =>
        _source.SaveAsync(values, cancellationToken);

    /// <inheritdoc/>
    protected override void NotifyValueProperties()
    {
        NotifyPropertyChanged(nameof(ChromiumState));
        NotifyPropertyChanged(nameof(Version));
        NotifyPropertyChanged(nameof(ChromiumSentence));
        NotifyPropertyChanged(nameof(FixAction));
        NotifyPropertyChanged(nameof(IsAcquisitionVisible));
        NotifyPropertyChanged(nameof(ExecutablePath));
        NotifyPropertyChanged(nameof(PolicyMode));
        NotifyPropertyChanged(nameof(AllowedHostsText));
        NotifyPropertyChanged(nameof(DeniedHostsText));
        NotifyPropertyChanged(nameof(DownloadMode));
        NotifyPropertyChanged(nameof(SandboxMode));
        NotifyPropertyChanged(nameof(PersistentProfile));
        NotifyPropertyChanged(nameof(AllowDispatchSessions));
        NotifyPropertyChanged(nameof(LocalOriginsTrusted));
        NotifyPropertyChanged(nameof(PolicyRiskText));
        NotifyPropertyChanged(nameof(LocalOriginsRiskText));
        NotifyDialog();
    }

    private void NotifyDialog()
    {
        NotifyPropertyChanged(nameof(Dialog));
        NotifyPropertyChanged(nameof(HasAcquisitionDialog));
        NotifyPropertyChanged(nameof(DialogText));
    }

    private static string[] SplitHosts(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return text.Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
