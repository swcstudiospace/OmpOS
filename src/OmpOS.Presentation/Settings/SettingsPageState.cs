namespace OmpOS.Presentation.Settings;

/// <summary>The one state a settings page is in. A head shows exactly one state at a time.</summary>
public enum SettingsPageState
{
    /// <summary>The page's values are loading.</summary>
    Loading = 0,

    /// <summary>The values loaded; the page shows its controls.</summary>
    Ready = 1,

    /// <summary>The values could not be loaded. A head shows <see cref="SettingsPageBase.LoadError"/>.</summary>
    Error = 2,
}
