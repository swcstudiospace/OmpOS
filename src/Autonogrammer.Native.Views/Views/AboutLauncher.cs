using System.Runtime.InteropServices;
using Avalonia.Controls;
using Autonogrammer.Presentation.About;

namespace Autonogrammer.Native.Views;

/// <summary>
/// The real <see cref="IAboutLauncher"/>: it builds the About window over the version and revision of this build and keeps one instance open.
/// The links open through a <see cref="SystemUrlLauncher"/> of the About window, unless the head supplies another <see cref="IUrlLauncher"/>.
/// </summary>
public sealed class AboutLauncher : IAboutLauncher
{
    private readonly AboutModel _model;
    private readonly IUrlLauncher? _urls;
    private AboutWindow? _window;

    /// <summary>Initializes a new instance of the <see cref="AboutLauncher"/> class over this build's own version information.</summary>
    public AboutLauncher()
        : this(AboutModel.ForAssembly(typeof(AboutLauncher).Assembly, DescribePlatform()), urls: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AboutLauncher"/> class.</summary>
    /// <param name="model">What the window shows.</param>
    /// <param name="urls">Opens the links, or null to use the system browser through the About window.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public AboutLauncher(AboutModel model, IUrlLauncher? urls)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _urls = urls;
    }

    /// <inheritdoc/>
    public void Show(Window? owner)
    {
        if (_window is { } open)
        {
            open.Activate();
            return;
        }

        var created = new AboutWindow(_model, _urls ?? new SystemUrlLauncher(() => _window));
        created.Closed += OnClosed;
        _window = created;
        if (owner is null)
        {
            created.Show();
            return;
        }

        created.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        created.Show(owner);
    }

    private static string DescribePlatform() =>
        RuntimeInformation.OSDescription + " (" + RuntimeInformation.OSArchitecture + "), " + RuntimeInformation.FrameworkDescription;

    private void OnClosed(object? sender, EventArgs e)
    {
        if (sender is AboutWindow window)
        {
            window.Closed -= OnClosed;
        }

        _window = null;
    }
}
