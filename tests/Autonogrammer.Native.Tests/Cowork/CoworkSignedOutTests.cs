using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Cowork;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Composition;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Accounts;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>A signed-out head keeps the switch and the Cowork panel, shows the signed-out sentence, and opens no Cowork folder.</summary>
public sealed class CoworkSignedOutTests
{
    private const string DockBodyName = "DockPanelBody";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A head with no stored account gives Cowork services, the switch is in the tree, the Cowork panel shows the signed-out sentence, and no cowork folder exists.</summary>
    [Fact]
    public async Task ASignedOutHeadShowsTheSwitchAndTheSignedOutPanelAndOpensNoCoworkFolder()
    {
        using var tree = new TempTree();
        var services = await HeadServices.CreateAsync(HeadOptions(tree), Token).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);
        var head = await AgentHead.CreateAsync(AgentLoopFixtures.Options(tree, null, services: services)).ConfigureAwait(true);
        await using var headScope = head.ConfigureAwait(true);
        new NativeCoworkAccountGate(services.AccountHost.Session).IsSignedIn.ShouldBeFalse("the fixture head has no stored account");

        var cowork = CoworkSurfaceServicesFactory.TryBuild(head);

        cowork.ShouldNotBeNull("a signed-out head with Cowork services still shows the switch");
        Directory.Exists(Path.Combine(tree.State, CoworkAccountFolder.RootName)).ShouldBeFalse("signed out, no registry or state file is opened or created");

        var session = TestAppBuilder.StartSession();
        await using var sessionScope = session.ConfigureAwait(true);
        await session.RunAsync(() =>
        {
            var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot: null, MotionPreference.System);
            var window = new MainWindow(panel, MotionPreference.System, cowork: cowork) { Width = 1280, Height = 800 };
            window.Show();
            ShellHarness.Settle(window);
            try
            {
                var host = window.Content.ShouldBeOfType<SurfaceHost>();
                host.Switch.IsSwitchVisible.ShouldBeTrue();
                Toggles(window).Count.ShouldBe(2);

                host.Switch.Select(SessionSurface.Cowork);
                ShellHarness.Settle(window);
                host.Switch.Selected.ShouldBe(SessionSurface.Cowork);
                var bodies = host.Cowork.ShouldNotBeNull().GetVisualDescendants().OfType<TextBlock>()
                    .Where(block => string.Equals(block.Name, DockBodyName, StringComparison.Ordinal))
                    .Select(block => block.Text)
                    .ToList();
                bodies.ShouldBe([AccountCopy.NotConfigured]);
            }
            finally
            {
                window.Close();
                panel.Dispose();
            }

            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    private static HeadServicesOptions HeadOptions(TempTree tree) => new()
    {
        StateRoot = tree.State,
        PluginsRoot = tree.Plugins,
        WorkspaceRoot = tree.Workspace,
        HomeDirectory = Path.Combine(tree.Root, "home"),
        ConfigDirectory = Path.Combine(tree.Root, "config"),
        GetVariable = static _ => null,
    };

    private static List<ToggleButton> Toggles(MainWindow window) =>
        [.. window.GetVisualDescendants().OfType<ToggleButton>().Where(toggle => Avalonia.Automation.AutomationProperties.GetName(toggle) is "Code, workspace" or "Cowork, workspace")];
}
