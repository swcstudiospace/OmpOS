using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

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
    private bool _awaitingTemplate;

    /// <summary>Creates a terminal view. It starts omp when it is loaded and its data context is an <see cref="OmpTerminalViewModel"/>.</summary>
    public OmpTerminalView()
    {
        InitializeComponent();

        RestartButton.Click += OnRestartClick;
        CloseButton.Click += OnCloseClick;
        Loaded += OnLoaded;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        ActualThemeVariantChanged += OnThemeVariantChanged;
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
    /// Connects the control to the model and starts omp when it is not running yet and the model allows it (<see cref="OmpTerminalViewModel.AutoStart"/>). The control creates its emulator when its
    /// template is applied, so this runs once the view is loaded, never in the constructor, and the first start uses the grid
    /// the control really has.
    /// </summary>
    private void Begin(OmpTerminalViewModel model)
    {
        if (_bridge is not null)
        {
            return;
        }

        if (!Output.GetVisualChildren().Any())
        {
            // The view is loaded but was never laid out, because a region above it is hidden (the Kanban tab shows instead of the thread), so
            // the control has no template and no emulator yet. Begin again once the control gets its template, never before.
            AwaitTemplate();
            return;
        }

        var bridge = new OmpTerminalPtyBridge(model.SetSize);
        _bridge = bridge;
        Output.AttachConnection(bridge);
        ApplyTheme();

        var terminal = Output.Terminal;
        model.SetSize(terminal.Cols, terminal.Rows);
        bridge.AttachSession(model.Session);

        if (model.State == OmpTerminalState.Idle && model.AutoStart)
        {
            _ = model.StartAsync();
        }
    }

    private void AwaitTemplate()
    {
        if (_awaitingTemplate)
        {
            return;
        }

        _awaitingTemplate = true;
        Output.TemplateApplied += OnOutputTemplateApplied;
    }

    private void StopAwaitingTemplate()
    {
        if (!_awaitingTemplate)
        {
            return;
        }

        _awaitingTemplate = false;
        Output.TemplateApplied -= OnOutputTemplateApplied;
    }

    private void OnOutputTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        StopAwaitingTemplate();

        // The emulator exists once the template pass is over, so begin on the next turn of the UI thread.
        Dispatcher.UIThread.Post(() =>
        {
            if (IsLoaded && _model is { } model)
            {
                Begin(model);
            }
        });
    }

    /// <summary>
    /// Stops omp and releases the control's connection when the view leaves the visual tree, unless the model asked to keep omp running while
    /// the view is cached: then the connection stays, so output keeps reaching the screen while another tab or session is shown.
    /// </summary>
    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_model is { KeepRunningWhenDetached: true })
        {
            return;
        }

        Teardown();
        if (_model is not null)
        {
            _ = _model.StopAsync();
        }
    }

    /// <summary>
    /// Releases the control's connection and the bridge of a view that a shell kept running while it was detached, because the session it
    /// showed is closed. The model stops omp; this view never does.
    /// </summary>
    public void Release() => Teardown();

    /// <summary>Releases the control's connection and the bridge. The session is not touched here: its model stops it.</summary>
    private void Teardown()
    {
        StopAwaitingTemplate();
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

    // The terminal control is not styled by the variant: its cursor colour and ANSI palette are values, so they are read again whenever
    // the variant changes (a saved Theme setting or an operating-system switch under System).
    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        if (_bridge is not null)
        {
            ApplyTheme();
        }
    }

    private void ApplyTheme()
    {
        ApplyCursorColour();
        ApplyPalette();
    }

    /// <summary>Gives the cursor the theme's text colour, because the control's own default is white, which a light theme hides.</summary>
    private void ApplyCursorColour()
    {
        if (this.TryFindResource("OmpOS.Color.TextPrimary", ActualThemeVariant, out var resource) && resource is ISolidColorBrush brush)
        {
            Output.CursorColor = brush.Color;
        }
    }

    private string? HexOf(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is ISolidColorBrush brush
            ? string.Create(CultureInfo.InvariantCulture, $"#{brush.Color.R:x2}{brush.Color.G:x2}{brush.Color.B:x2}")
            : null;

    /// <summary>Gives the emulator the sixteen ANSI colours of the variant, so omp's coloured output keeps its contrast in both themes.</summary>
    private void ApplyPalette()
    {
        var terminal = Output.Terminal;
        var theme = TerminalPalette.For(ActualThemeVariant);

        // ApplyTheme resets a colour the theme leaves unset to the emulator's own default (black on white), so the text, background and
        // cursor of the variant are always part of the theme.
        theme.Foreground = HexOf("OmpOS.Color.TextPrimary");
        theme.Background = HexOf("OmpOS.Color.SurfaceSunken");
        theme.Cursor = HexOf("OmpOS.Color.TextPrimary");
        theme.CursorAccent = HexOf("OmpOS.Color.SurfaceSunken");
        terminal.Options.Theme = theme;
        terminal.Colors.ApplyTheme(theme);
        Output.Refresh();
    }
}
