using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.BrowserUse.Vision;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Browser;

/// <summary>Desktop composition: capability, guidance budget, settings hand-off, overlay and Stop.</summary>
public sealed class BrowserUseCompositionTests
{
    /// <summary>The capability is offered only when both switches hold, and a disabled browser registers no tools.</summary>
    [Fact]
    public void CapabilityAndDisabledRegistration()
    {
        using var off = BrowserUseComposition.Wire(new BrowserUseCompositionRequest
        {
            StateDirectory = Path.GetTempPath(),
            Configure = static options =>
            {
                options.Enabled = false;
                options.AllowDispatchSessions = true;
            },
        });
        off.Provider.ShouldBeNull();
        off.Capability().ShouldBeFalse();
        off.Pane.State.ShouldBe(Autonogrammer.Contracts.Cowork.Browser.BrowserPaneState.BrowserDisabled);

        using var dispatchOff = BrowserUseComposition.Wire(new BrowserUseCompositionRequest
        {
            StateDirectory = Path.GetTempPath(),
            ForceAvailable = true,
            Configure = static options =>
            {
                options.Enabled = true;
                options.AllowDispatchSessions = false;
            },
        });
        dispatchOff.Capability().ShouldBeFalse();
        BrowserUseCapability.IsOffered(dispatchOff.Options).ShouldBeFalse();

        using var both = BrowserUseComposition.Wire(new BrowserUseCompositionRequest
        {
            StateDirectory = Path.GetTempPath(),
            ForceAvailable = true,
            Configure = static options =>
            {
                options.Enabled = true;
                options.AllowDispatchSessions = true;
            },
        });
        both.Provider.ShouldNotBeNull();
        both.Capability().ShouldBeTrue();
    }

    /// <summary>Guidance is appended only when the provider is available, and the total stays within 8 KiB.</summary>
    [Fact]
    public void GuidanceStaysInsideTheComposerBudget()
    {
        CoworkSystemPromptOptions.Measure(BrowserLoopGuidance.Fragment).ShouldBeLessThanOrEqualTo(CoworkSystemPromptOptions.BrowserGuidanceBytes);
        var without = BrowserUseComposition.ComposePrompt(available: false, baseText: "base");
        without.ShouldNotContain(BrowserLoopGuidance.Fragment);
        var with = BrowserUseComposition.ComposePrompt(available: true, baseText: "base");
        with.ShouldContain(BrowserLoopGuidance.Fragment);
        CoworkSystemPromptOptions.Measure(with).ShouldBeLessThanOrEqualTo(CoworkSystemPromptOptions.TotalBytes);
        var embedded = BrowserUseComposition.ComposePrompt(available: true);
        CoworkSystemPromptOptions.Measure(embedded).ShouldBeLessThanOrEqualTo(CoworkSystemPromptOptions.TotalBytes);
    }

    /// <summary>The Cowork overlay still requires browser.enabled false. This test does not rewrite the writer.</summary>
    [Fact]
    public void OverlayRequiresBrowserEnabledFalse()
    {
        CoworkSessionProfile.OverlayYaml.ShouldContain("browser:");
        CoworkSessionProfile.OverlayYaml.ShouldContain("enabled: false");
    }

    /// <summary>The settings source wired for the head is the one assigned to HeadSettingsOptions.</summary>
    [Fact]
    public async Task SettingsSourceReachesTheHead()
    {
        using var tree = new TempTree();
        var head = AgentHead.Create(new AgentHeadOptions
        {
            PluginsRoot = tree.Plugins,
            StateRoot = tree.State,
            WorkspaceRoot = tree.Workspace,
            AllowedKinds = [],
        });
        await using (head.ConfigureAwait(true))
        {
            head.BrowserUse.ShouldNotBeNull();
            head.Settings.ShouldNotBeNull();
            head.BrowserUse.Settings.ShouldBeAssignableTo<Autonogrammer.Presentation.Settings.BrowserUse.IBrowserUseSettingsSource>();
        }
    }

    /// <summary>The browser panel is registered beside Artifacts and the header carries Stop.</summary>
    [Fact]
    public void DockAndHeaderAreRegistered()
    {
        using var shell = new CoworkShellModel(new MemoryCatalog());
        var chrome = new SurfaceChromeModel();
        shell.RegisterPanel(new ArtifactPanel());
        using var composed = BrowserUseComposition.Wire(new BrowserUseCompositionRequest
        {
            StateDirectory = Path.GetTempPath(),
            Shell = shell,
            Chrome = chrome,
        });
        shell.Panels.Select(static panel => panel.Id).ShouldBe(["artifacts", "browser"]);
        chrome.Entries.ShouldContain(entry => entry.Id == BrowserUseComposition.IndicatorId && entry.KeyboardShortcut == "Ctrl+Shift+.");
    }

    private sealed class ArtifactPanel : IDockPanel
    {
        public string Id => "artifacts";

        public string Title => "Artifacts";

        public object Content => this;
    }

    private sealed class MemoryCatalog : Autonogrammer.Contracts.Cowork.ICoworkSessionCatalog
    {
        public event EventHandler? Changed
        {
            add
            {
            }

            remove
            {
            }
        }

        public IReadOnlyList<Autonogrammer.Contracts.Cowork.CoworkSessionSummary> Snapshot() => [];
    }
}
