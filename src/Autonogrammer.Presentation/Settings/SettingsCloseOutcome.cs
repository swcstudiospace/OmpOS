namespace Autonogrammer.Presentation.Settings;

/// <summary>The result of asking the settings window to close.</summary>
public enum SettingsCloseOutcome
{
    /// <summary>Nothing was unsaved; the window is closed.</summary>
    Closed = 0,

    /// <summary>Unsaved changes exist; the window stays open and shows the close prompt.</summary>
    ConfirmationRequired = 1,
}
