using Autonogrammer.Native.Views.Cowork.Artifacts;
using Autonogrammer.Native.Views.Dispatch;
using Autonogrammer.Presentation.Dispatch;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>Headless checks of named controls on the native artifact and approval surfaces.</summary>
public sealed class CoworkHeadlessAccessibilityTests
{
    private static readonly string[] ApprovalButtonNames = ["ApproveOnceButton", "DenyButton", "DenyAndPauseButton"];

    [Fact]
    public async Task ArtifactToolbarButtonsHaveAccessibleNames()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var view = new ArtifactPanelView();
            var window = new Window { Content = view };
            try
            {
                window.Show();
                ShellHarness.Settle(window);
                AutomationProperties.GetName(view).ShouldBe("Artifact");
                var buttons = view.Toolbar.GetVisualDescendants().OfType<Button>().ToArray();
                buttons.Length.ShouldBe(7);
                foreach (var button in buttons)
                {
                    AutomationProperties.GetName(button).ShouldNotBeNullOrWhiteSpace();
                }
            }
            finally
            {
                window.Close();
            }
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task ApprovalActionsHaveAccessibleNames()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var model = DispatchApprovalModel.From("approval-1", "Office Mac", "run", "computer.use", ["shell"], "Fix the build");
            var view = new DispatchApprovalView { DataContext = model };
            var window = new Window { Content = view };
            try
            {
                window.Show();
                ShellHarness.Settle(window);
                foreach (var name in ApprovalButtonNames)
                {
                    var button = view.FindControl<Button>(name).ShouldNotBeNull();
                    AutomationProperties.GetName(button).ShouldNotBeNullOrWhiteSpace();
                }
            }
            finally
            {
                window.Close();
            }
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
