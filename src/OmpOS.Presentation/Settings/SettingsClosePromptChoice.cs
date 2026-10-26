namespace OmpOS.Presentation.Settings;

/// <summary>The user's answer to the unsaved-changes prompt.</summary>
public enum SettingsClosePromptChoice
{
    /// <summary>Save the changes, then close.</summary>
    Save = 0,

    /// <summary>Throw the changes away and close.</summary>
    Discard = 1,

    /// <summary>Dismiss the prompt and keep the window open.</summary>
    KeepEditing = 2,
}
