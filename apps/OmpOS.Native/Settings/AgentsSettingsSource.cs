using OmpOS.Harness.Terminal;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Settings;

/// <summary>
/// The Agents page's values over the head's settings file. The saved auto-run choice is the one source of truth for the Terminal tool:
/// <see cref="ToTerminalSettings"/> builds the settings the tool reads each time it builds its panel. No environment variable overrides
/// it: OmpOS has none for the terminal, so the file alone decides.
/// </summary>
internal sealed class AgentsSettingsSource : IAgentsSettingsSource
{
    private readonly FileSettingsStore _store;

    /// <summary>Initializes a new instance of the <see cref="AgentsSettingsSource"/> class.</summary>
    /// <param name="store">The settings file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public AgentsSettingsSource(FileSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Builds the terminal settings from the settings file. A file that cannot be used gives the defaults, which never run a command without an approval.</summary>
    /// <param name="store">The settings file.</param>
    /// <returns>The settings the Terminal tool builds its panel with.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public static TerminalSettings ToTerminalSettings(FileSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        try
        {
            return new TerminalSettings { AutoRunEnabled = store.Read().TerminalAutoRun };
        }
        catch (SettingsSourceException)
        {
            return new TerminalSettings();
        }
    }

    /// <inheritdoc/>
    public Task<AgentsSettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        SettingsTask.ReadAsync(() => new AgentsSettingsValues(_store.Read().TerminalAutoRun));

    /// <inheritdoc/>
    public Task SaveAsync(AgentsSettingsValues values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        return SettingsTask.WriteAsync(() => _store.SetTerminalAutoRun(values.TerminalAutoRun));
    }
}
