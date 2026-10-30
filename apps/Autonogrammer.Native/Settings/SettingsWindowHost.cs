using Autonogrammer.Presentation.Settings;
using Avalonia.Controls;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// Keeps the one non-modal Settings window of the head. Asking for it while it is open brings it forward; asking again after it closed
/// builds a new model over new pages and a new window, so every page starts from what is saved then. Every page is loaded when the
/// window opens. The host closes the window when the main window closes.
/// </summary>
internal sealed class SettingsWindowHost
{
    private readonly Func<SettingsWindowModel> _modelFactory;
    private readonly Func<SettingsWindowModel, ISettingsWindowHandle> _windowFactory;
    private ISettingsWindowHandle? _open;
    private SettingsWindowModel? _openModel;

    /// <summary>Initializes a new instance of the <see cref="SettingsWindowHost"/> class.</summary>
    /// <param name="modelFactory">Builds the model over new pages.</param>
    /// <param name="windowFactory">Builds the window over a model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="modelFactory"/> or <paramref name="windowFactory"/> is null.</exception>
    public SettingsWindowHost(Func<SettingsWindowModel> modelFactory, Func<SettingsWindowModel, ISettingsWindowHandle> windowFactory)
    {
        ArgumentNullException.ThrowIfNull(modelFactory);
        ArgumentNullException.ThrowIfNull(windowFactory);
        _modelFactory = modelFactory;
        _windowFactory = windowFactory;
    }

    /// <summary>Gets a value indicating whether the window is open.</summary>
    public bool IsOpen => _open is not null;

    /// <summary>Gets the open window, or null.</summary>
    public ISettingsWindowHandle? OpenWindow => _open;

    /// <summary>Opens the window over an owner, or brings the open one forward, then loads the pages of a new window.</summary>
    /// <param name="owner">The window it opens over, or null.</param>
    /// <param name="cancellationToken">Cancels the loading of the pages.</param>
    /// <param name="section">The page to show, or null to keep the window's own choice (the first page of a new window). A section the window has no page for is ignored.</param>
    /// <returns>A task that completes when every page of a new window has loaded or failed to load. It completes at once when the window was already open.</returns>
    public async Task ShowAsync(Window? owner, CancellationToken cancellationToken, SettingsSection? section = null)
    {
        if (_open is { } existing)
        {
            Select(_openModel, section);
            existing.Activate();
            return;
        }

        var model = _modelFactory();
        Select(model, section);
        var handle = _windowFactory(model);
        _open = handle;
        _openModel = model;
        handle.Closed += OnClosed;
        try
        {
            handle.Show(owner);
        }
        catch (InvalidOperationException)
        {
            // A window that could not be shown is not open: the next request builds a new one.
            handle.Closed -= OnClosed;
            _open = null;
            _openModel = null;
            throw;
        }

        // The window shows its pages in their Loading state first; each page then fills itself or shows its own retry.
        await model.LoadAllAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closes the open window, dropping unsaved changes. The application is closing. It does nothing when no window is open.</summary>
    public void CloseForShutdown() => _open?.CloseDiscardingChanges();

    private static void Select(SettingsWindowModel? model, SettingsSection? section)
    {
        if (model is not null && section is { } wanted && model.Pages.Any(page => page.Section == wanted))
        {
            model.SelectSection(wanted);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (sender is ISettingsWindowHandle handle)
        {
            handle.Closed -= OnClosed;
            if (ReferenceEquals(_open, handle))
            {
                _open = null;
                _openModel = null;
            }
        }
    }
}
