using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Cowork.Browser;

namespace Autonogrammer.Native.Views.Browser;

/// <summary>
/// Dock content for the browser pane. Registration with <c>CoworkDockHost</c> is 72-08. The host keeps registration order, so Artifacts is registered first and this panel mounts beside it.
/// </summary>
public sealed class BrowserPanePanel : IDockPanel
{
    /// <summary>The tab title the Cowork dock strip shows.</summary>
    public const string TabTitle = "Browser";

    /// <summary>Creates the panel over the view model and the desktop view.</summary>
    /// <param name="model">The pane model. A remote head passes a feed whose state is unavailable.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public BrowserPanePanel(BrowserPaneViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        View = new BrowserPaneView();
        View.Attach(model);
    }

    /// <summary>Gets the view model.</summary>
    public BrowserPaneViewModel Model { get; }

    /// <summary>Gets the desktop view.</summary>
    public BrowserPaneView View { get; }

    /// <inheritdoc />
    public string Id => Model.Id;

    /// <inheritdoc />
    public string Title => TabTitle;

    /// <inheritdoc />
    public object Content => Model;
}
