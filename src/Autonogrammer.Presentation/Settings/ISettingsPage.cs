using System.ComponentModel;

namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// One page of the settings window. A page owns a draft of its values, reports whether the draft differs from the last saved
/// values, and saves, resets and discards it. Expected failures (an unreadable source, a rejected write) become page state and
/// inline errors; they are never thrown.
/// </summary>
public interface ISettingsPage : INotifyPropertyChanged
{
    /// <summary>Gets the section the page fills. Unique inside one window.</summary>
    SettingsSection Section { get; }

    /// <summary>Gets the navigation label.</summary>
    string Title { get; }

    /// <summary>Gets a value indicating whether the draft has unsaved edits.</summary>
    bool IsDirty { get; }

    /// <summary>Gets a value indicating whether an inline validation or save error is showing.</summary>
    bool HasError { get; }

    /// <summary>Loads the page's values. An expected failure puts the page in its error state instead of throwing.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the page is ready or has failed to load.</returns>
    Task LoadAsync(CancellationToken cancellationToken);

    /// <summary>Saves the draft.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>True when the draft was saved or there was nothing to save; false when an entry is rejected (inline errors show it).</returns>
    Task<bool> SaveAsync(CancellationToken cancellationToken);

    /// <summary>Returns the draft to the page's defaults. The saved values are unchanged until <see cref="SaveAsync"/>.</summary>
    void Reset();

    /// <summary>Returns the draft to the last saved values and clears inline errors.</summary>
    void Discard();
}
