using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>
/// The display switches of the window: Vim keys in the composer, line numbers in code blocks, thinking blocks, status indicators, the running time on the status line, and whether rewind works. Every
/// change is saved through <see cref="IDisplaySettingsStore"/>, so the slash commands (<c>/vim</c>, <c>/line-numbers</c>, <c>/show-thinking</c>, <c>/status-indicators</c>) and the Settings window share one state.
/// </summary>
/// <remarks>
/// <para>The model only holds the values and announces changes. Each view reads the switch it cares about: the composer reads <see cref="VimMode"/> as a flag and applies Vim keys itself, a code block reads <see cref="ShowLineNumbers"/>.</para>
/// <para>Not thread-safe: it runs on the UI context that owns the presentation models.</para>
/// </remarks>
public sealed class DisplaySettings : ObservableModel
{
    private readonly IDisplaySettingsStore _store;
    private DisplaySettingsValues _values = DisplaySettingsValues.Defaults;

    /// <summary>Creates the model with the defaults. Call <see cref="LoadAsync"/> to read the saved values.</summary>
    /// <param name="store">Keeps the values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public DisplaySettings(IDisplaySettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Gets the values now.</summary>
    public DisplaySettingsValues Values => _values;

    /// <summary>Gets whether the composer uses Vim keys. The composer reads this flag; the model does not interpret keys.</summary>
    public bool VimMode => _values.VimMode;

    /// <summary>Gets whether code blocks show line numbers.</summary>
    public bool ShowLineNumbers => _values.ShowLineNumbers;

    /// <summary>Gets whether the transcript shows thinking blocks.</summary>
    public bool ShowThinkingBlocks => _values.ShowThinkingBlocks;

    /// <summary>Gets whether the window shows status indicators.</summary>
    public bool ShowStatusIndicators => _values.ShowStatusIndicators;

    /// <summary>Gets whether the status line shows the running time.</summary>
    public bool ShowStatusLineRunningTime => _values.ShowStatusLineRunningTime;

    /// <summary>Gets whether <c>/rewind</c>, <c>/undo</c> and <c>/restore</c> work.</summary>
    public bool RewindEnabled => _values.RewindEnabled;

    /// <summary>Reads the saved values.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the values are current.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Take(await _store.LoadAsync(cancellationToken).ConfigureAwait(true));
    }

    /// <summary>Sets one switch and saves it.</summary>
    /// <param name="setting">The switch.</param>
    /// <param name="value">The new value.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes when the value is saved. When saving fails the switch keeps the new value for this run and the exception goes to the caller.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="setting"/> is not a defined value.</exception>
    /// <exception cref="IOException">The value could not be saved.</exception>
    public async Task SetAsync(DisplaySetting setting, bool value, CancellationToken cancellationToken)
    {
        var next = _values.With(setting, value);
        Take(next);
        await _store.SaveAsync(next, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Reads one switch.</summary>
    /// <param name="setting">The switch.</param>
    /// <returns>Its value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="setting"/> is not a defined value.</exception>
    public bool Get(DisplaySetting setting) => _values.Get(setting);

    private void Take(DisplaySettingsValues next)
    {
        var before = _values;
        _values = next;
        Announce(before.VimMode, next.VimMode, nameof(VimMode));
        Announce(before.ShowLineNumbers, next.ShowLineNumbers, nameof(ShowLineNumbers));
        Announce(before.ShowThinkingBlocks, next.ShowThinkingBlocks, nameof(ShowThinkingBlocks));
        Announce(before.ShowStatusIndicators, next.ShowStatusIndicators, nameof(ShowStatusIndicators));
        Announce(before.ShowStatusLineRunningTime, next.ShowStatusLineRunningTime, nameof(ShowStatusLineRunningTime));
        Announce(before.RewindEnabled, next.RewindEnabled, nameof(RewindEnabled));
        NotifyPropertyChanged(nameof(Values));
    }

    private void Announce(bool before, bool after, string name)
    {
        if (before != after)
        {
            NotifyPropertyChanged(name);
        }
    }
}
