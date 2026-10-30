using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Settings;
using Avalonia.Automation;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Runtime page: the idle stop entry and the retention entry of a <see cref="RuntimePage"/>. The model checks each entry as it is
/// typed; an entry that is rejected shows its message under the entry and keeps its text so it can be corrected.
/// </summary>
internal partial class RuntimePageView : SettingsPageViewBase
{
    private readonly ModelWatcher<RuntimeSettingsModel> _modelWatch;
    private bool _syncing;

    /// <summary>Creates a view with no page.</summary>
    public RuntimePageView()
    {
        InitializeComponent();
        _modelWatch = new ModelWatcher<RuntimeSettingsModel>(Refresh);
        IdleStopBox.TextChanged += OnIdleStopChanged;
        RetentionBox.TextChanged += OnRetentionChanged;
        Start();
    }

    /// <summary>Gets the idle stop entry.</summary>
    internal TextBox IdleStop => IdleStopBox;

    /// <summary>Gets the retention entry.</summary>
    internal TextBox Retention => RetentionBox;

    /// <summary>Gets the message under the idle stop entry.</summary>
    internal TextBlock IdleStopMessage => IdleStopError;

    /// <summary>Gets the message under the retention entry.</summary>
    internal TextBlock RetentionMessage => RetentionError;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    /// <inheritdoc />
    protected override void OnPageChanged(ISettingsPage? page) => _modelWatch.Set((page as RuntimePage)?.Model);

    /// <inheritdoc />
    protected override void OnRefresh(ISettingsPage page, PageStatus status)
    {
        if (page is not RuntimePage runtime)
        {
            return;
        }

        var model = runtime.Model;
        _syncing = true;
        try
        {
            SetText(IdleStopBox, model.IdleStopText);
            SetText(RetentionBox, model.RetentionText);
        }
        finally
        {
            _syncing = false;
        }

        ShowMessage(IdleStopError, model.IdleStopError);
        ShowMessage(RetentionError, model.RetentionError);
    }

    private static void SetText(TextBox box, string text)
    {
        if (!string.Equals(box.Text, text, StringComparison.Ordinal))
        {
            box.Text = text;
        }
    }

    private static void ShowMessage(TextBlock block, string? message)
    {
        block.Text = message ?? string.Empty;
        block.IsVisible = !string.IsNullOrEmpty(message);
        AutomationProperties.SetName(block, message ?? string.Empty);
    }

    private void OnIdleStopChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_syncing && Page is RuntimePage page)
        {
            _ = page.Model.SetIdleStop(IdleStopBox.Text);
            Refresh();
        }
    }

    private void OnRetentionChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_syncing && Page is RuntimePage page)
        {
            _ = page.Model.SetRetention(RetentionBox.Text);
            Refresh();
        }
    }
}
