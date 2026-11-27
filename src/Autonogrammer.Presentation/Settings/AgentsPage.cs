namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// The Agents page: the terminal auto-run switch was removed (Phase 81), so the page carries the one-time removal announcement and the note that
/// every tool call follows omp's approval policy. Every proposed shell-terminal command waits for an approval (approval-always); there is no
/// editable value left to save.
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

    /// <inheritdoc/>
    protected override Task<AgentsSettingsValues> LoadValuesAsync(CancellationToken cancellationToken) => _source.LoadAsync(cancellationToken);

    /// <inheritdoc/>
    protected override Task SaveValuesAsync(AgentsSettingsValues values, CancellationToken cancellationToken) =>
        _source.SaveAsync(values, cancellationToken);

    /// <inheritdoc/>
    protected override void NotifyValueProperties()
    {
        // The page carries no editable value after the auto-run switch was removed (Phase 81); there is no value property to announce.
    }
}
