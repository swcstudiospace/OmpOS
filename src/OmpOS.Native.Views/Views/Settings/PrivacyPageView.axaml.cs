using Avalonia.Controls;

namespace OmpOS.Native.Views.Settings;

/// <summary>The Privacy page: read-only statements about what OmpOS does with your code and prompts. It has no control to press.</summary>
internal partial class PrivacyPageView : SettingsPageViewBase
{
    /// <summary>Creates a view with no page.</summary>
    public PrivacyPageView()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets the list of statements.</summary>
    internal ItemsControl Statements => StatementRows;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;
}
