using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Cowork;
using Avalonia.Automation;
using Avalonia.Controls;

using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Shell;

/// <summary>One chrome entry appears in both top bars, in the header-end slot, and is a keyboard stop.</summary>
public sealed class CoworkChromeSlotTests
{
    /// <summary>Adding the entry once places the same label in both bars.</summary>
    [Fact]
    public async Task AnEntryAddedOnceAppearsInBothBars()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var catalog = new MemoryCoworkCatalog();
            using var shell = new CoworkShellModel(catalog);
            var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot: null, MotionPreference.System);
            var window = new MainWindow(panel, MotionPreference.System, cowork: new CoworkSurfaceServices { Catalog = catalog, Shell = shell })
            {
                Width = 1280,
                Height = 800,
            };
            try
            {
                window.Show();
                ShellHarness.Settle(window);
                var host = window.Content.ShouldBeOfType<SurfaceHost>();
                var clicks = 0;
                host.Chrome.Add(new SurfaceChromeEntry("devices", "Devices", () => clicks++));
                ShellHarness.Settle(window);
                var codeSlot = host.Code.FindControl<ContentControl>("HeaderEndSlot")!;
                var coworkSlot = host.Cowork!.HeaderSlot;
                var codeButton = ButtonNamed(codeSlot, "Devices");
                var coworkButton = ButtonNamed(coworkSlot, "Devices");
                codeButton.IsTabStop.ShouldBeTrue();
                coworkButton.IsTabStop.ShouldBeTrue();
                codeButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                clicks.ShouldBe(1);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }

    private static Button ButtonNamed(Control root, string name)
    {
        var slot = root.ShouldBeOfType<ContentControl>();
        var panel = slot.Content.ShouldBeAssignableTo<Panel>()!;
        return panel.Children.OfType<Button>().Single(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));
    }
}
