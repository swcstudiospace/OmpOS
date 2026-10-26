using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// The settings window state: the selected section, the Runtime section model and the close protocol. Closing with unsaved changes
/// does not close; it shows a prompt that the view answers through <see cref="ResolveClosePrompt"/>.
/// </summary>
public sealed class SettingsWindowModel : ObservableModel
{
    private SettingsSection _selectedSection = SettingsSection.Runtime;
    private bool _isClosePromptVisible;
    private bool _isClosed;

    /// <summary>Initializes a new instance of the <see cref="SettingsWindowModel"/> class.</summary>
    /// <param name="runtime">The Runtime section model.</param>
    public SettingsWindowModel(RuntimeSettingsModel runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Runtime = runtime;
    }

    /// <summary>Raised with the new values after the Runtime section is saved, so a head can persist them.</summary>
    public event EventHandler<RuntimeSettingsSavedEventArgs>? RuntimeSettingsSaved;

    /// <summary>Gets the Runtime section model.</summary>
    public RuntimeSettingsModel Runtime { get; }

    /// <summary>Gets the selected section.</summary>
    public SettingsSection SelectedSection => _selectedSection;

    /// <summary>Gets a value indicating whether the unsaved-changes prompt is showing.</summary>
    public bool IsClosePromptVisible => _isClosePromptVisible;

    /// <summary>Gets a value indicating whether the window has closed.</summary>
    public bool IsClosed => _isClosed;

    /// <summary>Gets a value indicating whether any section has unsaved changes.</summary>
    public bool HasUnsavedChanges => Runtime.IsDirty;

    /// <summary>Selects a section.</summary>
    /// <param name="section">The section to show.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="section"/> is not a defined section.</exception>
    public void SelectSection(SettingsSection section)
    {
        EnsureDefined(section);
        SetProperty(ref _selectedSection, section, nameof(SelectedSection));
    }

    /// <summary>Saves the Runtime section.</summary>
    /// <returns>True when the values were saved; false when an entry is rejected (the inline errors show it).</returns>
    public bool SaveRuntime()
    {
        if (Runtime.TrySave() is not { } saved)
        {
            return false;
        }

        NotifyPropertyChanged(nameof(HasUnsavedChanges));
        RuntimeSettingsSaved?.Invoke(this, new RuntimeSettingsSavedEventArgs(saved));
        return true;
    }

    /// <summary>Resets one section's draft to its defaults.</summary>
    /// <param name="section">The section to reset.</param>
    /// <returns>True when the section has settings and was reset; false for a section without runtime settings.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="section"/> is not a defined section.</exception>
    public bool ResetSection(SettingsSection section)
    {
        EnsureDefined(section);
        if (section != SettingsSection.Runtime)
        {
            return false;
        }

        Runtime.Reset();
        NotifyPropertyChanged(nameof(HasUnsavedChanges));
        return true;
    }

    /// <summary>Asks the window to close. With unsaved changes it stays open and shows the prompt first.</summary>
    /// <returns>Whether the window closed or the prompt is waiting for an answer.</returns>
    public SettingsCloseOutcome RequestClose()
    {
        if (_isClosed)
        {
            return SettingsCloseOutcome.Closed;
        }

        if (HasUnsavedChanges)
        {
            SetProperty(ref _isClosePromptVisible, true, nameof(IsClosePromptVisible));
            return SettingsCloseOutcome.ConfirmationRequired;
        }

        SetProperty(ref _isClosed, true, nameof(IsClosed));
        return SettingsCloseOutcome.Closed;
    }

    /// <summary>Answers the unsaved-changes prompt.</summary>
    /// <param name="choice">The user's choice.</param>
    /// <returns>True when the window closed. Saving a rejected entry returns false and keeps the window open with the inline error showing.</returns>
    /// <exception cref="InvalidOperationException">The prompt is not showing.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="choice"/> is not a defined choice.</exception>
    public bool ResolveClosePrompt(SettingsClosePromptChoice choice)
    {
        if (!Enum.IsDefined(choice))
        {
            throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unknown close prompt choice.");
        }

        if (!_isClosePromptVisible)
        {
            throw new InvalidOperationException("The unsaved-changes prompt is not showing.");
        }

        SetProperty(ref _isClosePromptVisible, false, nameof(IsClosePromptVisible));

        if (choice == SettingsClosePromptChoice.KeepEditing || (choice == SettingsClosePromptChoice.Save && !SaveRuntime()))
        {
            return false;
        }

        if (choice == SettingsClosePromptChoice.Discard)
        {
            Runtime.Discard();
            NotifyPropertyChanged(nameof(HasUnsavedChanges));
        }

        SetProperty(ref _isClosed, true, nameof(IsClosed));
        return true;
    }

    private static void EnsureDefined(SettingsSection section)
    {
        if (!Enum.IsDefined(section))
        {
            throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown settings section.");
        }
    }
}
