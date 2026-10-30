namespace Autonogrammer.Contracts.Cowork.Browser;

/// <summary>
/// State plus frames for one browser pane. Commands live on <see cref="IBrowserPaneControl"/>.
/// There is no relay implementation: Web and Android read <see cref="BrowserPaneState.RemoteViewUnavailable"/>.
/// </summary>
public interface IBrowserPaneFeed
{
    /// <summary>Gets the current pane state.</summary>
    BrowserPaneState State { get; }

    /// <summary>Gets the newest frame the viewer published, or null.</summary>
    BrowserFrame? LatestFrame { get; }

    /// <summary>Occurs after <see cref="State"/> or <see cref="LatestFrame"/> changes.</summary>
    event EventHandler? Changed;
}
