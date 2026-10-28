using Avalonia.Controls;
using Avalonia.Interactivity;
using OmpOS.Presentation.About;

namespace OmpOS.Native.Views;

/// <summary>
/// The content of the About window: the brand mark, the product name, the version, the revision and the licence of the running build, the AGPL-3.0
/// source offer, the links that open the source, the licence and the notice files in the system browser, and the Copy version info and Close buttons.
/// It draws the <see cref="AboutModel"/> it is built over and starts a browser only through the <see cref="IUrlLauncher"/> it is given.
/// </summary>
internal partial class AboutView : UserControl
{
    private readonly AboutModel _model;
    private readonly IUrlLauncher _launcher;
    private readonly IAboutClipboard _clipboard;

    /// <summary>Creates the view.</summary>
    /// <param name="model">The About model.</param>
    /// <param name="launcher">Opens the links.</param>
    /// <param name="clipboard">Receives the copied version information.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public AboutView(AboutModel model, IUrlLauncher launcher, IAboutClipboard clipboard)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(clipboard);

        _model = model;
        _launcher = launcher;
        _clipboard = clipboard;
        InitializeComponent();
        DataContext = model;
    }

    /// <summary>Raised when the user presses Close.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Gets the brand mark.</summary>
    internal Image Mark => MarkImage;

    /// <summary>Gets the list that holds one button per link.</summary>
    internal ItemsControl Links => LinkList;

    /// <summary>Gets the Copy version info button.</summary>
    internal Button Copy => CopyButton;

    /// <summary>Gets the Close button.</summary>
    internal Button Close => CloseButton;

    /// <summary>Gets the polite live region.</summary>
    internal TextBlock Status => StatusText;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnCopyClick(object? sender, RoutedEventArgs e) => _ = CopyAsync();

    private void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AboutLink link })
        {
            _ = OpenAsync(link);
        }
    }

    private async Task CopyAsync() =>
        _ = await _model.CopyVersionInfoAsync(_clipboard, CancellationToken.None).ConfigureAwait(true);

    private async Task OpenAsync(AboutLink link)
    {
        var opened = Uri.TryCreate(link.Location, UriKind.Absolute, out var uri)
            && await _launcher.OpenAsync(uri, CancellationToken.None).ConfigureAwait(true);
        if (!opened)
        {
            _model.ReportOpenFailed(link);
        }
    }
}
