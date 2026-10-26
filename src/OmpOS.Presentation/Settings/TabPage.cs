namespace OmpOS.Presentation.Settings;

/// <summary>
/// The Tab page: the read-only status of inline completion. It shows whether completion is on, the provider's host and port (never
/// a full URL or credentials) and how it is configured. It has nothing to edit, so it is never dirty and saving does nothing.
/// </summary>
public sealed class TabPage : SettingsPageBase
{
    private readonly ITabSettingsSource _source;
    private TabSettingsValues _values = new(Enabled: false, EndpointHost: null, Source: string.Empty);

    /// <summary>Initializes a new instance of the <see cref="TabPage"/> class.</summary>
    /// <param name="source">Where the status is read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public TabPage(ITabSettingsSource source)
        : base(SettingsSection.Tab, SettingsCopy.TabTitle, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets a value indicating whether inline completion is on.</summary>
    public bool IsEnabled => _values.Enabled;

    /// <summary>Gets the status line.</summary>
    public string StatusText => _values.Enabled ? SettingsCopy.TabStatusOn : SettingsCopy.TabStatusOff;

    /// <summary>Gets the provider's host and port, or a sentence that says none is configured or that the address is not shown.</summary>
    public string EndpointText =>
        EndpointDisplay.HostAndPort(_values.EndpointHost)
        ?? (_values.Enabled ? SettingsCopy.TabEndpointHidden : SettingsCopy.TabEndpointNone);

    /// <summary>Gets the sentence that says how inline completion is configured.</summary>
    public string HowConfiguredText => SettingsCopy.TabHowConfigured(_values.Enabled, _values.Source);

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(LoadCoreAsync, cancellationToken);

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        // The continuation must run on the context that owns the model, so the context is captured.
        var values = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        _values = values;
        NotifyPropertyChanged(nameof(IsEnabled));
        NotifyPropertyChanged(nameof(StatusText));
        NotifyPropertyChanged(nameof(EndpointText));
        NotifyPropertyChanged(nameof(HowConfiguredText));
    }
}
