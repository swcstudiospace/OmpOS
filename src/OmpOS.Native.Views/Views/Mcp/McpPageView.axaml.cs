using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmpOS.Presentation.Mcp;

namespace OmpOS.Native.Views.Mcp;

/// <summary>
/// The MCP settings page: configured servers by scope and file with their status, the configuration problems, the consent prompt, add-from-template
/// and an open-file button per file. The data context is a <see cref="McpSettingsViewModel"/>; the page starts loading it when the context is set. The
/// rows are rebuilt on every change, so the focused button is found again by its accessible name once the new rows are realized.
/// </summary>
/// <remarks>The page is not in the settings navigation yet: a head hosts it (see docs/reference/mcp.md, WIRING SPEC).</remarks>
internal partial class McpPageView : UserControl
{
    private McpSettingsViewModel? _model;

    /// <summary>Creates the page with no data context.</summary>
    public McpPageView()
    {
        InitializeComponent();
    }

    /// <summary>Binds the page to the model in the data context, starts loading, and keeps focus on the same button across redraws.</summary>
    /// <param name="e">The event data.</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.Changed -= OnModelChanged;
        }

        _model = DataContext as McpSettingsViewModel;
        if (_model is not null)
        {
            _model.Changed += OnModelChanged;

            // LoadAsync reports its failures in the model's state and never faults, so the task is not observed here.
            _ = _model.LoadAsync(CancellationToken.None);
        }
    }

    private void OnModelChanged(object? sender, EventArgs e)
    {
        var focused = FocusedButtonName();
        if (focused is null)
        {
            return;
        }

        // The rows are rebuilt after this event, which takes focus with them; the new buttons exist on a later dispatcher turn.
        Dispatcher.UIThread.Post(() => RestoreFocus(focused), DispatcherPriority.Background);
    }

    private string? FocusedButtonName()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Button;
        return focused is not null && IsInsidePage(focused) ? AutomationProperties.GetName(focused) : null;
    }

    private void RestoreFocus(string name)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control current && IsInsidePage(current))
        {
            return;
        }

        var target = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.IsVisible && string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));
        _ = target?.Focus();
    }

    private bool IsInsidePage(Visual visual) =>
        ReferenceEquals(visual, this) || visual.GetVisualAncestors().Contains(this);
}
