using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Views.Settings;

/// <summary>
/// The Display page: one switch for each display setting (vim keys, line numbers, thinking blocks, status indicators, running time and rewind),
/// each with its sentence. The switches are built once from the page's list; a switch is a draft until the footer's Save writes it.
/// </summary>
internal partial class DisplayPageView : SettingsPageViewBase
{
    private readonly List<CheckBox> _boxes = [];
    private bool _syncing;
    private bool _built;

    /// <summary>Creates a view with no page.</summary>
    public DisplayPageView()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets the switches in display order. They exist once the view has a page.</summary>
    internal IReadOnlyList<CheckBox> Switches => _boxes;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    /// <inheritdoc />
    protected override void OnRefresh(ISettingsPage page, PageStatus status)
    {
        if (page is not DisplaySettingsPage display)
        {
            return;
        }

        Build();
        _syncing = true;
        try
        {
            foreach (var box in _boxes)
            {
                if (box.Tag is DisplayToggle toggle)
                {
                    box.IsChecked = display.IsOn(toggle.Setting);
                }
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Build()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        foreach (var toggle in DisplaySettingsPage.Toggles)
        {
            var box = new CheckBox { Content = toggle.Label, Tag = toggle };
            AutomationProperties.SetName(box, toggle.Label);
            AutomationProperties.SetHelpText(box, toggle.Help);
            box.IsCheckedChanged += OnToggleChanged;
            _boxes.Add(box);

            var help = new TextBlock { Text = toggle.Help };
            help.Classes.Add("settings-help");
            TogglePanel.Children.Add(box);
            TogglePanel.Children.Add(help);
        }
    }

    private void OnToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || Page is not DisplaySettingsPage page || sender is not CheckBox { Tag: DisplayToggle toggle } box)
        {
            return;
        }

        page.SetToggle(toggle.Setting, box.IsChecked == true);
    }
}
