namespace Autonogrammer.Presentation.Settings.Customize;

/// <summary>The one state a Customize catalogue page is in. A head shows exactly one state at a time.</summary>
public enum CustomizePageState
{
    /// <summary>The catalogue is loading.</summary>
    Loading = 0,

    /// <summary>The catalogue loaded and lists at least one entry.</summary>
    Ready = 1,

    /// <summary>The catalogue loaded and lists nothing.</summary>
    Empty = 2,

    /// <summary>The catalogue could not be read. A head shows <see cref="CustomizeCatalogPage.LoadFailure"/>.</summary>
    Error = 3,
}
