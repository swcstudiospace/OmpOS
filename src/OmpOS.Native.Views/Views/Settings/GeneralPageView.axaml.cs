using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Views.Settings;

/// <summary>
/// The General page: the motion setting as a list of three choices, each with its label and its sentence, and the privacy statement as
/// selectable text. A choice is a draft until the footer's Save writes it.
/// </summary>
internal partial class GeneralPageView : SettingsPageViewBase
{
    private bool _syncing;
    private GeneralPage? _built;

    /// <summary>Creates a view with no page.</summary>
    public GeneralPageView()
    {
        InitializeComponent();
        MotionList.SelectionChanged += OnMotionSelectionChanged;
        Start();
    }

    /// <summary>Gets the list of motion choices.</summary>
    internal ListBox Motion => MotionList;

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
        MotionList.Items.Clear();
        foreach (var option in GeneralPage.MotionOptions)
        {
            var label = new TextBlock { Text = option.Label };
            label.Classes.Add("settings-label");
            var description = new TextBlock { Text = option.Description };
            description.Classes.Add("settings-help");
            var content = new StackPanel();
            content.Children.Add(label);
            content.Children.Add(description);

            var item = new ListBoxItem { Content = content, Tag = option };
            AutomationProperties.SetName(item, option.Label);
            AutomationProperties.SetHelpText(item, option.Description);
            MotionList.Items.Add(item);
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
}
