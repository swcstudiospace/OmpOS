namespace OmpOS.Presentation.Settings;

/// <summary>
/// The Plugins page: the four plugins (Ultrathink, Swarm, Substrate and GSD-Core) as a read-only list. They are always on and
/// connected to every omp process OmpOS starts; there is deliberately no switch (OQ-80), so the page has nothing to edit.
/// </summary>
public sealed class PluginsPage : SettingsPageBase
{
    private readonly IPluginsSettingsSource _source;
    private IReadOnlyList<PluginRow> _rows = [];

    /// <summary>Initializes a new instance of the <see cref="PluginsPage"/> class.</summary>
    /// <param name="source">Where the plugins are read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public PluginsPage(IPluginsSettingsSource source)
        : base(SettingsSection.Plugins, SettingsCopy.PluginsTitle, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the plugins in composition order.</summary>
    public IReadOnlyList<PluginRow> Rows => _rows;

    /// <summary>Gets a value indicating whether the page loaded and the source reported no plugin.</summary>
    public bool IsEmpty => State == SettingsPageState.Ready && _rows.Count == 0;

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(LoadCoreAsync, cancellationToken);

    /// <inheritdoc/>
    protected override void OnStateChanged() => NotifyPropertyChanged(nameof(IsEmpty));

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        // The continuation must run on the context that owns the model, so the context is captured.
        var values = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        _rows = [.. values.Rows];
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(IsEmpty));
    }
}
