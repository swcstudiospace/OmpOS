using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Cowork;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Shell;

/// <summary>The window shows the switch only when Cowork services are present, and settings still attach.</summary>
public sealed class SurfaceHostTests
{
    /// <summary>A window with Cowork services shows the switch. A window without shows Code only and no switch toggle.</summary>
    [Fact]
    public async Task TheSwitchExistsOnlyWhenCoworkServicesArePresent()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var barePanel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot: null, MotionPreference.System);
            var bare = Show(barePanel, cowork: null);
            try
            {
                bare.Content.ShouldBeOfType<SurfaceHost>().Cowork.ShouldBeNull();
                Toggles(bare).ShouldBeEmpty();
                ShellHarness.MainViewOf(bare).ShouldBeSameAs(((SurfaceHost)bare.Content!).Code);
            }
            finally
            {
                bare.Close();
                barePanel.Dispose();
            }

            var catalog = new MemoryCoworkCatalog();
            using var shell = new CoworkShellModel(catalog);
            var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot: null, MotionPreference.System);
            var with = Show(panel, new CoworkSurfaceServices { Catalog = catalog, Shell = shell });
            try
            {
                var host = with.Content.ShouldBeOfType<SurfaceHost>();
                host.Cowork.ShouldNotBeNull();
                Toggles(with).Count.ShouldBe(2);
                host.Switch.IsSwitchVisible.ShouldBeTrue();
            }
            finally
            {
                with.Close();
                panel.Dispose();
            }

            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }

    /// <summary>Settings attach through the host, and the harness still finds the main view.</summary>
    [Fact]
    public async Task SettingsAttachThroughTheHost()
    {
        using var tree = new TempTree();
        var engines = new SessionEngineSet(new SessionEngineSetOptions
        {
            Loop = AgentLoopEnvironment.Resolve(static _ => null, tree.Workspace, tree.Workspace, tree.Root),
            AllowedKinds = [],
        });
        var store = new FileSettingsStore(tree.State);
        var settings = HeadSettings.Create(engines, store, TimeProvider.System, dispatch: null, new HeadSettingsOptions { GetVariable = static _ => null });
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var session = TestAppBuilder.StartSession();
            await session.RunAsync(() =>
            {
                var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot: null, MotionPreference.System);
                var window = Show(panel, cowork: null);
                try
                {
                    settings.Attach(window);
                    ShellHarness.MainViewOf(window).ShouldNotBeNull();
                }
                finally
                {
                    window.Close();
                    panel.Dispose();
                }

                return Task.CompletedTask;
            }, token).ConfigureAwait(true);
        }
        finally
        {
            await settings.DisposeAsync().ConfigureAwait(true);
            await engines.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static MainWindow Show(Autonogrammer.Presentation.AgentPanel.AgentPanelViewModel panel, CoworkSurfaceServices? cowork)
    {
        var window = new MainWindow(panel, MotionPreference.System, cowork: cowork) { Width = 1280, Height = 800 };
        window.Show();
        ShellHarness.Settle(window);
        return window;
    }

    private static List<ToggleButton> Toggles(MainWindow window) =>
        [.. window.GetVisualDescendants().OfType<ToggleButton>().Where(toggle => Avalonia.Automation.AutomationProperties.GetName(toggle) is "Code, workspace" or "Cowork, workspace")];
}
