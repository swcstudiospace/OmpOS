namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>The one state the Models page is in. A head shows exactly one state at a time.</summary>
public enum ModelsPageState
{
    /// <summary>The providers are loading.</summary>
    Loading = 0,

    /// <summary>The providers loaded and the page shows them.</summary>
    Ready = 1,

    /// <summary>The source listed no provider.</summary>
    Empty = 2,

    /// <summary>The providers could not be read. A head shows <see cref="ModelsSettingsPage.LoadFailure"/>.</summary>
    Error = 3,
}
