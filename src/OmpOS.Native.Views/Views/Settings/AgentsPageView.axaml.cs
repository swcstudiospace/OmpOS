using Avalonia.Controls;
using Avalonia.Interactivity;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Views.Settings;

/// <summary>
/// The Agents page: the switch that lets proposed terminal commands run without asking, off by default, with a warning while it is on
/// and the note that every tool call follows omp's approval policy. The switch is a draft until the footer's Save writes it.
/// </summary>
internal partial class AgentsPageView : SettingsPageViewBase
{
    private bool _syncing;

    /// <summary>Creates a view with no page.</summary>
    public AgentsPageView()
    {
        InitializeComponent();
        AutoRunBox.IsCheckedChanged += OnAutoRunChanged;
        Start();
    }

    /// <summary>Gets the auto-run switch.</summary>
    internal CheckBox AutoRun => AutoRunBox;

    /// <summary>Gets the warning shown while the switch is on.</summary>
    internal TextBlock Warning => AutoRunWarning;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    /// <inheritdoc />
    protected override void OnRefresh(ISettingsPage page, PageStatus status)
    {
        if (page is not AgentsPage agents)
        {
            return;
        }

        _syncing = true;
        try
        {
            AutoRunBox.IsChecked = agents.TerminalAutoRun;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnAutoRunChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing && Page is AgentsPage page)
        {
            page.SetTerminalAutoRun(AutoRunBox.IsChecked == true);
        }
    }
}
