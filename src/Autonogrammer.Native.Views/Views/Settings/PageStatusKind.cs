namespace Autonogrammer.Native.Views.Settings;

/// <summary>The one thing a settings page shows instead of its controls.</summary>
internal enum PageStatusKind
{
    /// <summary>The page shows its controls.</summary>
    Ready = 0,

    /// <summary>The page's values are loading.</summary>
    Loading = 1,

    /// <summary>The page's values could not be loaded; the page offers Try again.</summary>
    Error = 2,
}
