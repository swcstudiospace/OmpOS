using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Shell;

/// <summary>A switch hides one surface and shows the other. Neither stack is replaced, stopped or disposed.</summary>
public sealed class SurfaceSwitchTests
{
    /// <summary>Both views stay the same instances, and the shell still accepts a panel after the switch.</summary>
    [Fact]
    public async Task BothSurfacesStayAliveAcrossSwitches()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var catalog = new MemoryCoworkCatalog();
            var shell = new CoworkShellModel(catalog);
            var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot: null, MotionPreference.System);
            var selected = panel.Selected;
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
                var code = host.Code;
                var cowork = host.Cowork.ShouldNotBeNull();
                host.Switch.Select(SessionSurface.Cowork);
                ShellHarness.Settle(window);
                host.Code.ShouldBeSameAs(code);
                host.Cowork.ShouldBeSameAs(cowork);
                code.IsEffectivelyVisible.ShouldBeFalse();
                cowork.IsEffectivelyVisible.ShouldBeTrue();
                panel.Selected.ShouldBeSameAs(selected);
                shell.RegisterPanel(new DockPanel());
                shell.Panels.Count.ShouldBe(1);
                host.Switch.Select(SessionSurface.Code);
                ShellHarness.Settle(window);
                code.IsEffectivelyVisible.ShouldBeTrue();
                cowork.IsEffectivelyVisible.ShouldBeFalse();
                panel.Selected.ShouldBeSameAs(selected);
                shell.Panels.Count.ShouldBe(1);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }

    private sealed class DockPanel : IDockPanel
    {
        public string Id => "alive";

        public string Title => "Alive";

        public object Content { get; } = new Autonogrammer.Native.Views.Cowork.CoworkDockContent("still here");
    }
}
