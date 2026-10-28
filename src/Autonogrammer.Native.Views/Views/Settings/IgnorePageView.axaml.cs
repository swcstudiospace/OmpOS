using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Ignore;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Ignore page: the global pattern list and the two switches you edit, the built-in list (hidden until you ask for it) and the ignore files found
/// in the workspace. Everything you edit is a draft until the footer's Save writes it. The rows are drawn from the page each time it changes.
/// </summary>
internal partial class IgnorePageView : SettingsPageViewBase
{
    private string _patternKey = string.Empty;
    private string _fileKey = string.Empty;
    private bool _defaultsBuilt;
    private bool _syncing;

    /// <summary>Creates a view with no page.</summary>
    public IgnorePageView()
    {
        InitializeComponent();
        UseDefaultBox.IsCheckedChanged += OnUseDefaultChanged;
        HierarchicalBox.IsCheckedChanged += OnHierarchicalChanged;
        Start();
    }

    /// <summary>Gets the box a pattern is typed in.</summary>
    internal TextBox Pattern => PatternBox;

    /// <summary>Gets the Add pattern button.</summary>
    internal Button Add => AddButton;

    /// <summary>Gets the list of patterns of the draft.</summary>
    internal Panel Patterns => PatternList;

    /// <summary>Gets the built-in list switch.</summary>
    internal CheckBox UseDefault => UseDefaultBox;

    /// <summary>Gets the hierarchical switch.</summary>
    internal CheckBox Hierarchical => HierarchicalBox;

    /// <summary>Gets the button that shows or hides the built-in list.</summary>
    internal Button ToggleDefaults => DefaultsToggle;

    /// <summary>Gets the panel that holds the built-in list.</summary>
    internal Panel Defaults => DefaultsPanel;

    /// <summary>Gets the list of ignore files found in the workspace.</summary>
    internal Panel Files => FileList;

    /// <summary>Gets the line that says why a pattern was refused.</summary>
    internal TextBlock EntryError => EntryErrorText;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    /// <inheritdoc />
    protected override void OnRefresh(ISettingsPage page, PageStatus status)
    {
        if (page is not IgnoreSettingsPage ignore)
        {
            return;
        }

        _syncing = true;
        try
        {
            UseDefaultBox.IsChecked = ignore.UseDefaultList;
            HierarchicalBox.IsChecked = ignore.HierarchicalIgnoreFiles;
        }
        finally
        {
            _syncing = false;
        }

        DrawPatterns(ignore);
        DrawFiles(ignore);
        if (DefaultsPanel.IsVisible)
        {
            BuildDefaults(ignore);
        }
    }

    private static TextBlock Text(string text, string classes)
    {
        var block = new TextBlock { Text = text };
        foreach (var name in classes.Split(' '))
        {
            block.Classes.Add(name);
        }

        return block;
    }

    private void DrawPatterns(IgnoreSettingsPage page)
    {
        var key = string.Join('\n', page.Patterns);
        if (string.Equals(key, _patternKey, StringComparison.Ordinal) && PatternList.Children.Count > 0)
        {
            return;
        }

        _patternKey = key;
        PatternList.Children.Clear();
        if (page.Patterns.Count == 0)
        {
            PatternList.Children.Add(Text(IgnoreCopy.EmptyList, "muted settings-body"));
            return;
        }

        foreach (var pattern in page.Patterns)
        {
            PatternList.Children.Add(PatternRow(pattern));
        }
    }

    private Border PatternRow(string pattern)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        grid.Children.Add(Text(pattern, "settings-mono"));
        var button = new Button { Content = IgnoreCopy.RemoveLabel };
        _ = button.Bind(MinHeightProperty, button.GetResourceObservable("Autonogrammer.Size.TargetMin"));
        _ = button.Bind(MinWidthProperty, button.GetResourceObservable("Autonogrammer.Size.TargetMin"));
        AutomationProperties.SetName(button, IgnoreCopy.RemoveName(pattern));
        button.Click += (_, _) =>
        {
            if (Page is IgnoreSettingsPage page && page.RemovePattern(pattern))
            {
                PatternBox.FocusWhenReady();
            }
        };
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        var border = new Border { Child = grid };
        border.Classes.Add("settings-row");
        AutomationProperties.SetName(border, pattern);
        return border;
    }

    private void DrawFiles(IgnoreSettingsPage page)
    {
        var key = string.Join('\n', page.Files.Select(static file => string.Join('\t', [file.Summary, .. file.Problems])));
        if (string.Equals(key, _fileKey, StringComparison.Ordinal) && FileList.Children.Count > 0)
        {
            return;
        }

        _fileKey = key;
        FileList.Children.Clear();
        if (page.Files.Count == 0)
        {
            FileList.Children.Add(Text(IgnoreCopy.NoFiles, "muted settings-body"));
            return;
        }

        foreach (var file in page.Files)
        {
            var stack = new StackPanel();
            stack.Children.Add(Text(file.Summary, "settings-label"));
            foreach (var problem in file.Problems)
            {
                stack.Children.Add(Text(IgnoreCopy.ProblemLine(problem), "tone-warning settings-help"));
            }

            var border = new Border { Child = stack };
            border.Classes.Add("settings-row");
            AutomationProperties.SetName(border, file.Summary);
            FileList.Children.Add(border);
        }
    }

    private void BuildDefaults(IgnoreSettingsPage page)
    {
        if (_defaultsBuilt)
        {
            return;
        }

        _defaultsBuilt = true;
        DefaultsList.Children.Clear();
        foreach (var pattern in page.DefaultPatterns)
        {
            DefaultsList.Children.Add(Text(pattern, "settings-mono"));
        }
    }

    private void OnDefaultsToggleClick(object? sender, RoutedEventArgs e)
    {
        var show = !DefaultsPanel.IsVisible;
        if (show && Page is IgnoreSettingsPage page)
        {
            BuildDefaults(page);
        }

        DefaultsPanel.IsVisible = show;
        var label = show ? IgnoreCopy.HideDefaultsLabel : IgnoreCopy.ShowDefaultsLabel;
        DefaultsToggle.Content = label;
        AutomationProperties.SetName(DefaultsToggle, label);
    }

    private void OnUseDefaultChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing && Page is IgnoreSettingsPage page)
        {
            page.SetUseDefaultList(UseDefaultBox.IsChecked == true);
        }
    }

    private void OnHierarchicalChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing && Page is IgnoreSettingsPage page)
        {
            page.SetHierarchicalIgnoreFiles(HierarchicalBox.IsChecked == true);
        }
    }

    private void OnAddClick(object? sender, RoutedEventArgs e) => Submit();

    private void OnPatternKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Submit();
        }
    }

    private void Submit()
    {
        if (Page is IgnoreSettingsPage page && page.AddPattern(PatternBox.Text))
        {
            PatternBox.Text = string.Empty;
        }

        PatternBox.FocusWhenReady();
    }
}
