using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Views.Settings;

/// <summary>
/// The General page: the theme and motion settings as lists of three choices, each with its label and its sentence, and the privacy
/// statement as selectable text. A choice is a draft until the footer's Save writes it; the running window follows a saved theme at once.
/// </summary>
internal partial class GeneralPageView : SettingsPageViewBase
{
    private bool _syncing;
    private GeneralPage? _built;

    /// <summary>Creates a view with no page.</summary>
    public GeneralPageView()
    {
        InitializeComponent();
        ThemeList.SelectionChanged += OnThemeSelectionChanged;
        MotionList.SelectionChanged += OnMotionSelectionChanged;
        AboutButton.Click += OnAboutClick;
        Start();
    }

    /// <summary>Gets the list of theme choices.</summary>
    internal ListBox ThemeChoices => ThemeList;

    /// <summary>Gets the list of motion choices.</summary>
    internal ListBox Motion => MotionList;

    /// <summary>Gets the button that opens the About window. It is hidden when the head has none.</summary>
    internal Button About => AboutButton;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    /// <inheritdoc />
    protected override void OnRefresh(ISettingsPage page, PageStatus status)
    {
        if (page is not GeneralPage general)
        {
            return;
        }

        Build(general);
        _syncing = true;
        try
        {
            ThemeList.SelectedItem = ThemeList.Items
                .OfType<ListBoxItem>()
                .FirstOrDefault(item => item.Tag is ThemeOption option && option.Value == general.Theme);
            MotionList.SelectedItem = MotionList.Items
                .OfType<ListBoxItem>()
                .FirstOrDefault(item => item.Tag is MotionOption option && option.Value == general.Motion);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Build(GeneralPage page)
    {
        if (ReferenceEquals(_built, page))
        {
            return;
        }

        _built = page;
        Fill(ThemeList, GeneralPage.ThemeOptions.Select(static option => (option.Label, option.Description, (object)option)));
        Fill(MotionList, GeneralPage.MotionOptions.Select(static option => (option.Label, option.Description, (object)option)));
    }

    private static void Fill(ListBox list, IEnumerable<(string Label, string Description, object Tag)> choices)
    {
        list.Items.Clear();
        foreach (var (text, sentence, tag) in choices)
        {
            var label = new TextBlock { Text = text };
            label.Classes.Add("settings-label");
            var description = new TextBlock { Text = sentence };
            description.Classes.Add("settings-help");
            var content = new StackPanel();
            content.Children.Add(label);
            content.Children.Add(description);

            var item = new ListBoxItem { Content = content, Tag = tag };
            AutomationProperties.SetName(item, text);
            AutomationProperties.SetHelpText(item, sentence);
            list.Items.Add(item);
        }
    }

    private void OnThemeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || Page is not GeneralPage page)
        {
            return;
        }

        if (ThemeList.SelectedItem is ListBoxItem { Tag: ThemeOption option })
        {
            page.SetTheme(option.Value);
        }
    }

    private void OnMotionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || Page is not GeneralPage page)
        {
            return;
        }

        if (MotionList.SelectedItem is ListBoxItem { Tag: MotionOption option })
        {
            page.SetMotion(option.Value);
        }
    }

    private void OnAboutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Page is GeneralPage page)
        {
            page.OpenAbout();
        }
    }
}
