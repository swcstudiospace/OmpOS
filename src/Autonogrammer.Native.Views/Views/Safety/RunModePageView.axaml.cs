using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Safety;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Views.Safety;

/// <summary>
/// The Run mode page: the three modes with what each does in Autonogrammer, the classifier note, the allowlists and read boundary you can edit, then
/// the rules, files and problems in force. Everything is a draft until the footer's Save writes it. The lists of rows are drawn from the page each time it changes.
/// </summary>
internal partial class RunModePageView : SettingsPageViewBase
{
    private readonly Dictionary<Panel, string> _drawn = [];
    private bool _syncing;
    private RunModeSettingsPage? _built;

    /// <summary>Creates a view with no page.</summary>
    public RunModePageView()
    {
        InitializeComponent();
        ModeList.SelectionChanged += OnModeChanged;
        BoundaryList.SelectionChanged += OnBoundaryChanged;
        TerminalAdd.Click += OnTerminalAdd;
        McpAdd.Click += OnMcpAdd;
        ReadPathAdd.Click += OnReadPathAdd;
        TerminalBox.KeyDown += OnTerminalKey;
        McpBox.KeyDown += OnMcpKey;
        ReadPathBox.KeyDown += OnReadPathKey;
        Start();
    }

    /// <summary>Gets the list of run modes.</summary>
    internal ListBox Modes => ModeList;

    /// <summary>Gets the list of read boundaries.</summary>
    internal ListBox Boundaries => BoundaryList;

    /// <summary>Gets the box for a new terminal entry.</summary>
    internal TextBox TerminalEntryBox => TerminalBox;

    /// <summary>Gets the button that adds a terminal entry.</summary>
    internal Button AddTerminal => TerminalAdd;

    /// <summary>Gets the box for a new MCP entry.</summary>
    internal TextBox McpEntryBox => McpBox;

    /// <summary>Gets the button that adds an MCP entry.</summary>
    internal Button AddMcp => McpAdd;

    /// <summary>Gets the box for a new read path.</summary>
    internal TextBox ReadPathEntryBox => ReadPathBox;

    /// <summary>Gets the button that adds a read path.</summary>
    internal Button AddReadPathButton => ReadPathAdd;

    /// <summary>Gets the block that explains why the classifier is not available.</summary>
    internal Border Classifier => ClassifierBox;

    /// <summary>Gets the warning shown while Run Everything is selected.</summary>
    internal TextBlock RunEverythingNote => RunEverythingWarning;

    /// <summary>Gets the message of a rejected entry.</summary>
    internal TextBlock EntryError => EntryErrorText;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    /// <inheritdoc />
    protected override void OnRefresh(ISettingsPage page, PageStatus status)
    {
        if (page is not RunModeSettingsPage run)
        {
            return;
        }

        Build(run);
        _syncing = true;
        try
        {
            ModeList.SelectedItem = ModeList.Items.OfType<ListBoxItem>().FirstOrDefault(item => item.Tag is RunModeOption option && option.Choice == run.Mode);
            BoundaryList.SelectedItem = BoundaryList.Items.OfType<ListBoxItem>().FirstOrDefault(item => item.Tag is ReadBoundaryChoice choice && choice == run.ReadBoundary);
        }
        finally
        {
            _syncing = false;
        }

        BoundaryList.IsEnabled = run.CanEditSandbox;
        Draw(TerminalList, run.TerminalRows, text => run.RemoveTerminalEntry(text), TerminalBox);
        Draw(McpList, run.McpRows, text => run.RemoveMcpEntry(text), McpBox);
        Draw(ReadPathList, run.ReadPathRows, text => run.RemoveReadPath(text), ReadPathBox);
        Draw(AllowRuleList, run.AllowRules, null, null);
        Draw(DenyRuleList, run.DenyRules, null, null);
        Draw(AllowInstructionList, run.AllowInstructions, null, null);
        Draw(BlockInstructionList, run.BlockInstructions, null, null);
        DrawFiles(run);
        DrawProblems(run);
    }

    private static TextBlock Text(string text, string role)
    {
        var block = new TextBlock { Text = text };
        block.Classes.Add(role);
        return block;
    }

    private static void FillChoices(ListBox list, IEnumerable<(string Label, string Sentence, object Tag)> choices)
    {
        list.Items.Clear();
        foreach (var (label, sentence, tag) in choices)
        {
            var content = new StackPanel();
            content.Children.Add(Text(label, "settings-label"));
            content.Children.Add(Text(sentence, "settings-help"));
            var item = new ListBoxItem { Content = content, Tag = tag };
            AutomationProperties.SetName(item, label);
            AutomationProperties.SetHelpText(item, sentence);
            list.Items.Add(item);
        }
    }

    private void Build(RunModeSettingsPage page)
    {
        if (ReferenceEquals(_built, page))
        {
            return;
        }

        _built = page;
        FillChoices(ModeList, RunModeSettingsPage.Options.Select(static option => (option.Title, option.Summary, (object)option)));
        FillChoices(
            BoundaryList,
            [
                (SafetyCopy.BoundarySystem, SafetyCopy.BoundarySystemHelp, ReadBoundaryChoice.System),
                (SafetyCopy.BoundaryWorkspace, SafetyCopy.BoundaryWorkspaceHelp, ReadBoundaryChoice.Workspace),
            ]);
    }

    private void Draw(Panel host, IReadOnlyList<RunModeEntry> rows, Func<string, bool>? remove, TextBox? afterRemove)
    {
        var key = string.Join('\n', rows.Select(static row => row.Source + "|" + row.Text + "|" + (row.IsEditable ? "e" : "r")));
        if (_drawn.TryGetValue(host, out var previous) && string.Equals(previous, key, StringComparison.Ordinal))
        {
            return;
        }

        _drawn[host] = key;
        host.Children.Clear();
        if (rows.Count == 0)
        {
            host.Children.Add(Text(SafetyCopy.EmptyList, "muted settings-body"));
            return;
        }

        foreach (var row in rows)
        {
            host.Children.Add(Row(row, remove, afterRemove));
        }
    }

    private static Border Row(RunModeEntry entry, Func<string, bool>? remove, TextBox? afterRemove)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        var text = new StackPanel();
        text.Children.Add(Text(entry.Text, "settings-mono"));
        if (entry.Source.Length > 0)
        {
            text.Children.Add(Text(entry.Source, "settings-help"));
        }

        grid.Children.Add(text);
        if (entry.IsEditable && remove is not null)
        {
            var button = new Button { Content = SafetyCopy.RemoveLabel };
            _ = button.Bind(MinHeightProperty, button.GetResourceObservable("Autonogrammer.Size.TargetMin"));
            _ = button.Bind(MinWidthProperty, button.GetResourceObservable("Autonogrammer.Size.TargetMin"));
            AutomationProperties.SetName(button, SafetyCopy.RemoveLabel + " " + entry.Text);
            button.Click += (_, _) =>
            {
                if (remove(entry.Text))
                {
                    afterRemove.FocusWhenReady();
                }
            };
            Grid.SetColumn(button, 1);
            grid.Children.Add(button);
        }

        var border = new Border { Child = grid };
        border.Classes.Add("settings-row");
        AutomationProperties.SetName(border, entry.Summary);
        return border;
    }

    private void DrawFiles(RunModeSettingsPage page)
    {
        var key = string.Join('\n', page.Files.Select(static file => file.Summary));
        if (_drawn.TryGetValue(FileList, out var previous) && string.Equals(previous, key, StringComparison.Ordinal))
        {
            return;
        }

        _drawn[FileList] = key;
        FileList.Children.Clear();
        foreach (var file in page.Files)
        {
            var content = new StackPanel();
            content.Children.Add(Text(file.Scope + " " + file.Kind, "settings-label"));
            content.Children.Add(Text(file.Path, "settings-mono"));
            content.Children.Add(Text(file.Status + (file.IsWinning ? " It sets a value in force." : string.Empty), "settings-help"));
            var border = new Border { Child = content };
            border.Classes.Add("settings-row");
            AutomationProperties.SetName(border, file.Summary);
            FileList.Children.Add(border);
        }
    }

    private void DrawProblems(RunModeSettingsPage page)
    {
        var key = string.Join('\n', page.Diagnostics.Select(static problem => problem.Summary));
        if (_drawn.TryGetValue(ProblemList, out var previous) && string.Equals(previous, key, StringComparison.Ordinal))
        {
            return;
        }

        _drawn[ProblemList] = key;
        ProblemList.Children.Clear();
        foreach (var problem in page.Diagnostics)
        {
            var content = new StackPanel();
            content.Children.Add(Text(problem.File, "settings-mono"));
            content.Children.Add(Text(problem.Message, problem.IsError ? "tone-error settings-body" : "tone-warning settings-body"));
            var border = new Border { Child = content };
            border.Classes.Add("settings-row");
            AutomationProperties.SetName(border, problem.Summary);
            ProblemList.Children.Add(border);
        }
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && Page is RunModeSettingsPage page && ModeList.SelectedItem is ListBoxItem { Tag: RunModeOption option })
        {
            page.SetMode(option.Choice);
        }
    }

    private void OnBoundaryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && Page is RunModeSettingsPage page && BoundaryList.SelectedItem is ListBoxItem { Tag: ReadBoundaryChoice choice })
        {
            page.SetReadBoundary(choice);
        }
    }

    private void OnTerminalAdd(object? sender, RoutedEventArgs e) => Submit(TerminalBox, static (page, text) => page.AddTerminalEntry(text));

    private void OnMcpAdd(object? sender, RoutedEventArgs e) => Submit(McpBox, static (page, text) => page.AddMcpEntry(text));

    private void OnReadPathAdd(object? sender, RoutedEventArgs e) => Submit(ReadPathBox, static (page, text) => page.AddReadPath(text));

    private void OnTerminalKey(object? sender, KeyEventArgs e) => SubmitOnEnter(e, TerminalBox, static (page, text) => page.AddTerminalEntry(text));

    private void OnMcpKey(object? sender, KeyEventArgs e) => SubmitOnEnter(e, McpBox, static (page, text) => page.AddMcpEntry(text));

    private void OnReadPathKey(object? sender, KeyEventArgs e) => SubmitOnEnter(e, ReadPathBox, static (page, text) => page.AddReadPath(text));

    private void SubmitOnEnter(KeyEventArgs e, TextBox box, Func<RunModeSettingsPage, string?, bool> add)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Submit(box, add);
        }
    }

    private void Submit(TextBox box, Func<RunModeSettingsPage, string?, bool> add)
    {
        if (Page is RunModeSettingsPage page && add(page, box.Text))
        {
            box.Text = string.Empty;
        }

        box.FocusWhenReady();
    }
}
