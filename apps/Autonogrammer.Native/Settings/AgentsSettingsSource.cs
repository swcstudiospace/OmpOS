using Autonogrammer.Harness.Terminal;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// The Agents page's values over the head's settings file. The terminal auto-run switch was removed (Phase 81), so the page carries no
/// editable value: <see cref="ToTerminalSettings"/> always builds the settings the Terminal tool reads with auto-run off, which maps the
/// removed toggle to approval-always for the shell terminal — every proposed command waits for an approval. A stored <c>terminal</c>
/// section in an existing settings file is parse-and-ignore data; it never takes effect.
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

    /// <summary>Builds the terminal settings for the Terminal tool. Approval-always since the auto-run switch was removed (Phase 81).</summary>
    /// <param name="store">The settings file. Ignored: the removed toggle no longer reads it.</param>
    /// <returns>The settings the Terminal tool builds its panel with.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public static TerminalSettings ToTerminalSettings(FileSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return new TerminalSettings();
    }

    /// <inheritdoc/>
    public Task<AgentsSettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        SettingsTask.ReadAsync(() =>
        {
            _ = _store.Read();
            return AgentsSettingsValues.Defaults;
        });

    /// <inheritdoc/>
    public Task SaveAsync(AgentsSettingsValues values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Task.CompletedTask;
    }
}
