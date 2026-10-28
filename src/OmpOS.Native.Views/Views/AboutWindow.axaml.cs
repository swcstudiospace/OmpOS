using Avalonia.Controls;
using Avalonia.Input;
using OmpOS.Presentation.About;

namespace OmpOS.Native.Views;

/// <summary>
/// The About window: a small window over <see cref="AboutView"/>. Esc and the Close button close it. It is not modal, and the
/// <see cref="AboutLauncher"/> holds one instance of it. It starts no browser of its own: the links go through the <see cref="IUrlLauncher"/> it is given.
/// </summary>
internal partial class AboutWindow : Window
{
    private readonly AboutView _view;

    /// <summary>Creates the window over a model.</summary>
    /// <param name="model">The About model.</param>
    /// <param name="launcher">Opens the links.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public AboutWindow(AboutModel model, IUrlLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(launcher);

        InitializeComponent();
        _view = new AboutView(model, launcher, new AvaloniaAboutClipboard(() => this));
        _view.CloseRequested += OnCloseRequested;
        Content = _view;
    }

    /// <summary>Gets the content view.</summary>
    internal AboutView View => _view;

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Close();
        }
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _view.Copy.Focus();
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();
}
