using System.Collections;
using Autonogrammer.Workspace;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views.Workspace;

/// <summary>
/// The workspace conventions panel: the rules with the malformed files, the skills, and the modes with the mode picker.
/// It draws the bound <see cref="WorkspaceConventionsModel"/>, redraws when the catalog reloads, and sends the mode choice
/// to the model. The picker and the mode list always show the same choice.
/// </summary>
internal partial class WorkspaceConventionsView : UserControl
{
    private readonly HashSet<string> _movedLegacyPaths = new(StringComparer.Ordinal);
    private readonly TextBlock _moveStatus = new() { IsVisible = false, TextWrapping = TextWrapping.Wrap };
    private WorkspaceConventionsModel? _model;
    private bool _rendering;

    /// <summary>Creates the panel with no data context.</summary>
    public WorkspaceConventionsView()
    {
        InitializeComponent();
        ModePicker.SelectionChanged += OnModeSelectionChanged;
        ModeRows.SelectionChanged += OnModeSelectionChanged;
        if (NoticeRows.Parent is Panel panel)
        {
            panel.Children.Insert(panel.Children.IndexOf(NoticeRows) + 1, _moveStatus);
        }
    }

    /// <summary>Gets the last command the copy affordance stored.</summary>
    internal string? LastCopiedCommand { get; private set; }

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
        if (Dispatcher.UIThread.CheckAccess())
        {
            Render();
            return;
        }

        Dispatcher.UIThread.Post(Render);
    }

    private void OnModeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_rendering || _model is null || sender is not SelectingItemsControl control)
        {
            return;
        }

        _model.SelectMode(control.SelectedItem as WorkspaceMode);
        Render();
    }

    private void Render()
    {
        var snapshot = _model?.Snapshot ?? WorkspaceSnapshot.Empty;
        var selected = _model?.SelectedMode;
        _rendering = true;
        try
        {
            var notices = (_model?.Notices ?? []).Where(notice => !_movedLegacyPaths.Contains(notice.LegacyPath));
            SetItems(NoticeRows, notices.Select(notice => new LegacyConventionNoticeRow(notice, this)).ToList());
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

    /// <summary>Copies a repository move command. Git is never started.</summary>
    /// <param name="command">The command text.</param>
    internal async Task CopyRepoCommandAsync(string command)
    {
        LastCopiedCommand = command;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(command).ConfigureAwait(true);
        }
    }

    /// <summary>Moves the user-scope legacy directory and shows what happened. A refusal or a failure is shown, never dropped.</summary>
    /// <param name="home">The user home.</param>
    /// <returns>The move's result, or null when the panel has no model or the move failed.</returns>
    internal LegacyMoveResult? MoveUser(string home)
    {
        if (_model is null)
        {
            return null;
        }

        LegacyMoveResult result;
        try
        {
            result = _model.MoveUserLegacyDirectory(home);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowMoveStatus("Move failed. Nothing was moved: " + exception.Message);
            return null;
        }

        if (result.Status is LegacyMoveStatus.Moved or LegacyMoveStatus.AlreadyMigrated)
        {
            _movedLegacyPaths.Add(result.LegacyPath);
            Render();
        }

        ShowMoveStatus(result.Status == LegacyMoveStatus.Moved ? "Moved " + result.LegacyPath + " to " + result.NewPath + "." : result.Message ?? string.Empty);
        return result;
    }

    /// <summary>Gets the text of the move status line. Empty when no move has been reported.</summary>
    internal string MoveStatusText => _moveStatus.Text ?? string.Empty;

    private void ShowMoveStatus(string text)
    {
        _moveStatus.Text = text;
        _moveStatus.IsVisible = text.Length > 0;
    }

    private static void SetItems(ItemsControl target, IEnumerable items)
    {
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
