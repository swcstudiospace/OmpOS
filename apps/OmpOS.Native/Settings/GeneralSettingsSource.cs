using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Settings;

/// <summary>The General page's values over the head's settings file: the motion and theme settings are read from it and saved to it. The shell applies a saved choice through <see cref="FileSettingsStore.Changed"/>.</summary>
internal sealed class GeneralSettingsSource : IGeneralSettingsSource
{
    private readonly FileSettingsStore _store;

    /// <summary>Initializes a new instance of the <see cref="GeneralSettingsSource"/> class.</summary>
    /// <param name="store">The settings file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public GeneralSettingsSource(FileSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc/>
    public Task<GeneralSettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        SettingsTask.ReadAsync(() => ToValues(_store.Read()));

    /// <inheritdoc/>
    public Task SaveAsync(GeneralSettingsValues values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        return SettingsTask.WriteAsync(() => _store.SetGeneral(values.Motion, values.Theme));
    }

    private static GeneralSettingsValues ToValues(StoredSettings stored) => new(stored.Motion, stored.Theme);
}
