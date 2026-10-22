using System.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using OmpOS.Workspace;

namespace OmpOS.Native.Views.Workspace;

/// <summary>
/// The workspace conventions panel: the rules with the malformed files, the skills, and the modes with the mode picker.
/// It draws the bound <see cref="WorkspaceConventionsModel"/>, redraws when the catalog reloads, and sends the mode choice
/// to the model. The picker and the mode list always show the same choice.
/// </summary>
internal partial class WorkspaceConventionsView : UserControl
{
    private WorkspaceConventionsModel? _model;
    private bool _rendering;

    /// <summary>Creates the panel with no data context.</summary>
    public WorkspaceConventionsView()
    {
        InitializeComponent();
        ModePicker.SelectionChanged += OnModeSelectionChanged;
        ModeRows.SelectionChanged += OnModeSelectionChanged;
    }

    /// <summary>Binds the panel to the model in the data context and draws its current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.Changed -= OnModelChanged;
        }

        _model = DataContext as WorkspaceConventionsModel;
        if (_model is not null)
        {
            _model.Changed += OnModelChanged;
        }

        Render();
    }

    private void OnModelChanged(object? sender, EventArgs e)
    {
        // The catalog may reload on a watcher thread; the controls are drawn only on the thread that owns them.
        if (Dispatcher.UIThread.CheckAccess())
        {
            Render();
        }
        else
        {
            Dispatcher.UIThread.Post(() => Render());
        }
    }

    private void OnModeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_rendering || _model is null)
        {
            return;
        }

        var chosen = (sender as SelectingItemsControl)?.SelectedItem as WorkspaceMode;
        _model.SelectMode(chosen);
        Render();
    }

    private void Render()
    {
        var snapshot = _model?.Snapshot ?? WorkspaceSnapshot.Empty;
        var selected = _model?.SelectedMode;

        _rendering = true;
        try
        {
            SetItems(RuleRows, snapshot.Rules);
            SetItems(SkillRows, snapshot.Skills);
            SetItems(DiagnosticRows, snapshot.Diagnostics);
            SetItems(ModePicker, snapshot.Modes);
            SetItems(ModeRows, snapshot.Modes);

            SetSelection(ModePicker, selected);
            SetSelection(ModeRows, selected);
        }
        finally
        {
            _rendering = false;
        }

        RulesEmptyText.IsVisible = snapshot.Rules.Count == 0;
        SkillsEmptyText.IsVisible = snapshot.Skills.Count == 0;
        DiagnosticsEmptyText.IsVisible = snapshot.Diagnostics.Count == 0;
        ModesEmptyText.IsVisible = snapshot.Modes.Count == 0;
        SelectedModeDetail.Text = DescribeMode(selected);
    }

    private static void SetItems(ItemsControl target, IEnumerable items)
    {
        // Assign only a new collection: resetting an unchanged list would drop the focused row.
        if (!ReferenceEquals(target.ItemsSource, items))
        {
            target.ItemsSource = items;
        }
    }

    private static void SetSelection(SelectingItemsControl target, WorkspaceMode? selected)
    {
        if (!Equals(target.SelectedItem, selected))
        {
            target.SelectedItem = selected;
        }
    }

    private static string DescribeMode(WorkspaceMode? mode) =>
        mode is null
            ? "No mode is selected."
            : "Rules: " + JoinOrNone(mode.Rules) + ". Skills: " + JoinOrNone(mode.Skills) + ".";

    private static string JoinOrNone(IReadOnlyList<string> ids) => ids.Count == 0 ? "none" : string.Join(", ", ids);
}
