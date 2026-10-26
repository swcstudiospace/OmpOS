namespace OmpOS.Presentation.Settings;

/// <summary>
/// The Agents page: the terminal auto-run switch, off by default, and a read-only note that every tool call follows omp's approval
/// policy. Changing the switch is a draft until <see cref="EditableSettingsPage{TValues}.SaveAsync"/> writes it through the source; it is not instant.
/// </summary>
public sealed class AgentsPage : EditableSettingsPage<AgentsSettingsValues>
{
    private readonly IAgentsSettingsSource _source;

    /// <summary>Initializes a new instance of the <see cref="AgentsPage"/> class.</summary>
    /// <param name="source">Where the values are read and written.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public AgentsPage(IAgentsSettingsSource source)
        : base(SettingsSection.Agents, SettingsCopy.AgentsTitle, AgentsSettingsValues.Defaults)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets a value indicating whether the draft lets terminal commands run without an approval.</summary>
    public bool TerminalAutoRun => Draft.TerminalAutoRun;

    /// <summary>Gets a value indicating whether the auto-run warning is showing: it shows while the draft turns auto-run on.</summary>
    public bool IsAutoRunWarningVisible => Draft.TerminalAutoRun;

    /// <summary>Sets the auto-run switch in the draft. Ignored while the page is not ready.</summary>
    /// <param name="enabled">True to run proposed terminal commands without an approval.</param>
    public void SetTerminalAutoRun(bool enabled) => SetDraft(Draft with { TerminalAutoRun = enabled });

    /// <inheritdoc/>
    protected override Task<AgentsSettingsValues> LoadValuesAsync(CancellationToken cancellationToken) => _source.LoadAsync(cancellationToken);

    /// <inheritdoc/>
    protected override Task SaveValuesAsync(AgentsSettingsValues values, CancellationToken cancellationToken) =>
        _source.SaveAsync(values, cancellationToken);

    /// <inheritdoc/>
    protected override void NotifyValueProperties()
    {
        NotifyPropertyChanged(nameof(TerminalAutoRun));
        NotifyPropertyChanged(nameof(IsAutoRunWarningVisible));
    }
}
