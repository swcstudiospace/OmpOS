using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace OmpOS.Native.Views.Terminal;

/// <summary>
/// The interactive omp terminal. It hosts the terminal control, draws omp's output in it, sends keys and paste to omp's
/// session, and keeps omp's pseudo-terminal the size of the control's grid. Its data context is an
/// <see cref="OmpTerminalViewModel"/>, which owns the omp session: when omp exits the status line says
/// "omp exited with code N" in a polite live region and Restart omp starts it again; when omp is unavailable the fix text
/// is shown instead of the terminal. Leaving the visual tree stops omp, so no omp child remains.
/// </summary>
internal partial class OmpTerminalView : UserControl
{
    /// <summary>The text that resets the emulator (the terminal full reset), so a restarted omp starts on a clean screen.</summary>
    private const string FullReset = "\u001bc";

    private OmpTerminalViewModel? _model;
    private OmpTerminalPtyBridge? _bridge;

    /// <summary>Creates a terminal view. It starts omp when it is loaded and its data context is an <see cref="OmpTerminalViewModel"/>.</summary>
    public OmpTerminalView()
    {
        InitializeComponent();

        RestartButton.Click += OnRestartClick;
        CloseButton.Click += OnCloseClick;
        Loaded += OnLoaded;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        Refresh();
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Bind(DataContext as OmpTerminalViewModel);
    }

    private void Bind(OmpTerminalViewModel? model)
    {
        if (ReferenceEquals(_model, model))
        {
            return;
        }

        var previous = _model;
        if (previous is not null)
        {
            previous.PropertyChanged -= OnModelPropertyChanged;
            previous.OutputReceived -= OnOutputReceived;
            previous.SessionChanged -= OnSessionChanged;
            previous.ScreenResetRequested -= OnScreenResetRequested;
        }

        _model = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelPropertyChanged;
            model.OutputReceived += OnOutputReceived;
            model.SessionChanged += OnSessionChanged;
            model.ScreenResetRequested += OnScreenResetRequested;
        }

        Refresh();
        if (IsLoaded)
        {
            // The view is already showing a terminal: end the old model's omp and connect the new model.
            Teardown();
            if (previous is not null)
            {
                _ = previous.StopAsync();
            }

            if (model is not null)
            {
                Begin(model);
            }
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_model is not null)
        {
            Begin(_model);
        }
    }

    /// <summary>
    /// Connects the control to the model and starts omp when it is not running yet. The control creates its emulator when its
    /// template is applied, so this runs once the view is loaded, never in the constructor, and the first start uses the grid
    /// the control really has.
    /// </summary>
    private void Begin(OmpTerminalViewModel model)
    {
        if (_bridge is not null)
        {
            return;
        }

        var bridge = new OmpTerminalPtyBridge(model.SetSize);
        _bridge = bridge;
        Output.AttachConnection(bridge);
        ApplyCursorColour();

        var terminal = Output.Terminal ?? throw new InvalidOperationException("The terminal control has no emulator to draw into.");
        model.SetSize(terminal.Cols, terminal.Rows);
        bridge.AttachSession(model.Session);

        if (model.State == OmpTerminalState.Idle)
        {
            _ = model.StartAsync();
        }
    }

    /// <summary>Stops omp and releases the control's connection when the view leaves the visual tree.</summary>
    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Teardown();
        if (_model is not null)
        {
            _ = _model.StopAsync();
        }
    }

    /// <summary>Releases the control's connection and the bridge. The session is not touched here: its model stops it.</summary>
    private void Teardown()
    {
        var bridge = Interlocked.Exchange(ref _bridge, null);
        if (bridge is null)
        {
            return;
        }

        Output.DetachConnection();
        bridge.Dispose();
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e) => _model?.RestartCommand.Execute(null);

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _model?.CloseCommand.Execute(null);

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Refresh();
        if (string.Equals(e.PropertyName, nameof(OmpTerminalViewModel.State), StringComparison.Ordinal) && _model is { State: OmpTerminalState.Running })
        {
            Output.Focus();
        }
    }

    /// <summary>Hands output from the session's reader thread to the control, which reads it on its own thread.</summary>
    private void OnOutputReceived(object? sender, OmpTerminalOutputEventArgs e) => Volatile.Read(ref _bridge)?.Write(e.Text);

    private void OnSessionChanged(object? sender, OmpTerminalSessionEventArgs e) => Volatile.Read(ref _bridge)?.AttachSession(e.Session);

    private void OnScreenResetRequested(object? sender, EventArgs e) => Volatile.Read(ref _bridge)?.Write(FullReset);

    private void Refresh()
    {
        var model = _model;
        var unavailable = model is { State: OmpTerminalState.Unavailable };
        StatusText.Text = model?.StatusText ?? string.Empty;
        FixText.Text = model?.FixText ?? string.Empty;
        UnavailablePanel.IsVisible = unavailable;
        Output.IsVisible = !unavailable;
        RestartButton.IsVisible = model is { CanRestart: true };
        CloseButton.IsEnabled = model is not null;
    }

    /// <summary>Gives the cursor the theme's text colour, because the control's own default is white, which a light theme hides.</summary>
    private void ApplyCursorColour()
    {
        if (this.TryFindResource("OmpOS.Color.TextPrimary", ActualThemeVariant, out var resource) && resource is ISolidColorBrush brush)
        {
            Output.CursorColor = brush.Color;
        }
    }
}
