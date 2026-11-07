using Autonogrammer.Presentation.Cowork;
using Avalonia.Controls;

using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>
/// The right dock: a tab strip when two or more panels are registered, and one mapped view. One panel shows no strip.
/// </summary>
public partial class CoworkDockHost : UserControl
{
    private CoworkShellModel? _shell;

    /// <summary>Creates the dock with no shell.</summary>
    public CoworkDockHost()
    {
        InitializeComponent();
    }

    /// <summary>Gets the tab strip. It is hidden while fewer than two panels are registered.</summary>
    public StackPanel Tabs => DockTabs;

    /// <summary>Gets the panel content host.</summary>
    public ContentControl PanelHost => DockContent;

    /// <summary>Binds the dock to the shell.</summary>
    /// <param name="shell">The shell that owns the panel list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public void Attach(CoworkShellModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        if (_shell is not null)
        {
            _shell.Changed -= OnChanged;
        }

        _shell = shell;
        shell.Changed += OnChanged;
        Rebuild();
    }

    private void OnChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        if (_shell is null)
        {
            return;
        }

        DockTabs.Children.Clear();
        DockTabs.IsVisible = _shell.ShowTabStrip;
        foreach (var panel in _shell.Panels)
        {
            var tab = new ToggleButton
            {
                Content = panel.Title,
                IsChecked = string.Equals(panel.Id, _shell.SelectedPanelId, StringComparison.Ordinal),
                MinHeight = 32,
                MinWidth = 32,
                IsTabStop = true,
                Focusable = true,
                Tag = panel.Id,
            };
            Avalonia.Automation.AutomationProperties.SetName(tab, panel.Title);
            tab.Click += OnTabClick;
            DockTabs.Children.Add(tab);
        }

        var selected = _shell.SelectedPanel;
        DockContent.Content = selected is null ? null : ViewMapping.Create(selected.Content);
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: string id })
        {
            _shell?.SelectPanel(id);
        }
    }
}
