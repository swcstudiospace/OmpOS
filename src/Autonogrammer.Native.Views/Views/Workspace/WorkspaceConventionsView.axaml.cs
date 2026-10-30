using System.Collections;
using System.Windows.Input;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Workspace;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views.Workspace;

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
            SetItems(NoticeRows, (_model?.Notices ?? []).Select(notice => new LegacyConventionNoticeRow(notice, this)).ToList());
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

    internal void MoveUser(string home) => _model?.MoveUserLegacyDirectory(home);

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

    /// <summary>One legacy notice row. The accessible name carries the severity and the path.</summary>
    internal sealed class LegacyConventionNoticeRow
    {
        private readonly WorkspaceConventionsView _view;

        /// <summary>Creates a row.</summary>
        /// <param name="notice">The notice.</param>
        /// <param name="view">The view that copies or moves.</param>
        public LegacyConventionNoticeRow(LegacyNotice notice, WorkspaceConventionsView view)
        {
            Notice = notice;
            _view = view;
            var severity = notice.Conflict ? "Warning" : "Note";
            Text = notice.LegacyPath + ": " + notice.MoveCommand;
            Name = severity + ". " + Text;
            ButtonText = string.Equals(notice.Scope, "user", StringComparison.Ordinal) ? "Move" : "Copy command";
            Command = new NoticeCommand(this);
        }

        /// <summary>Gets the notice.</summary>
        public LegacyNotice Notice { get; }

        /// <summary>Gets the sentence.</summary>
        public string Text { get; }

        /// <summary>Gets the accessible name: severity, path and command.</summary>
        public string Name { get; }

        /// <summary>Gets the button label.</summary>
        public string ButtonText { get; }

        /// <summary>Gets the button command. A project scope copies; it never runs git.</summary>
        public ICommand Command { get; }

        private sealed class NoticeCommand(LegacyConventionNoticeRow row) : ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter)
            {
                if (string.Equals(row.Notice.Scope, "user", StringComparison.Ordinal))
                {
                    var home = Directory.GetParent(row.Notice.LegacyPath)?.FullName;
                    if (home is not null)
                    {
                        row._view.MoveUser(home);
                    }

                    return;
                }

                _ = row._view.CopyRepoCommandAsync(row.Notice.MoveCommand);
            }
        }
    }
}
