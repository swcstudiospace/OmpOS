using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>
/// The display switches of the window: line numbers in code blocks, status indicators, the running time on the status line, and whether rewind works. Every
/// change is saved through <see cref="IDisplaySettingsStore"/>, so the slash commands (<c>/line-numbers</c>, <c>/status-indicators</c>) and the Settings window share one state.
/// </summary>
/// <remarks>
/// <para>The model only holds the values and announces changes. Each view reads the switch it cares about: a code block reads <see cref="ShowLineNumbers"/>.</para>
/// <para>Vim keys and thinking blocks are still stored, but no view reads them yet, so neither the window nor a slash command offers them.</para>
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
    /// <returns>A task that completes when the value is saved. The switch changes only after the save succeeds; when saving fails it keeps its previous value and the exception goes to the caller.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="setting"/> is not a defined value.</exception>
    /// <exception cref="IOException">The value could not be saved.</exception>
    public async Task SetAsync(DisplaySetting setting, bool value, CancellationToken cancellationToken)
    {
        var next = _values.With(setting, value);
        await _store.SaveAsync(next, cancellationToken).ConfigureAwait(true);
        Take(next);
    }

    /// <summary>Saves every switch the draft changed from the baseline, in one write, on top of the current values.</summary>
    /// <param name="baseline">The values the draft was made from.</param>
    /// <param name="draft">The values to save.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes when the changed switches are saved. Switches the draft did not change keep their current values, including changes made since the draft was made. When saving fails nothing changes.</returns>
    /// <exception cref="IOException">The values could not be saved.</exception>
    public async Task SaveChangesAsync(DisplaySettingsValues baseline, DisplaySettingsValues draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(draft);
        var next = _values;
        foreach (var setting in Enum.GetValues<DisplaySetting>())
        {
            if (draft.Get(setting) != baseline.Get(setting))
            {
                next = next.With(setting, draft.Get(setting));
            }
        }

        if (next == _values)
        {
            return;
        }

        await _store.SaveAsync(next, cancellationToken).ConfigureAwait(true);
        Take(next);
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
