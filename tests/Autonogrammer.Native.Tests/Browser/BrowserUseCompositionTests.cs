using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.BrowserUse.Vision;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Presentation.Cowork;
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

    /// <summary>The async factory leaves the browser settings and call gate usable until the head releases its retained owner during awaited shutdown.</summary>
    [Fact]
    public async Task BrowserSettingsAndGateLiveUntilTheHeadIsDisposed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = await AgentHead.CreateAsync(new AgentHeadOptions
            {
                PluginsRoot = tree.Plugins,
                StateRoot = tree.State,
                WorkspaceRoot = tree.Workspace,
                AllowedKinds = [],
            }).ConfigureAwait(true);
            await using var headLifetime = head.ConfigureAwait(true);
            var browser = head.BrowserUse.ShouldNotBeNull();
            head.Settings.ShouldNotBeNull();
            browser.Settings.ShouldBeAssignableTo<Autonogrammer.Presentation.Settings.BrowserUse.IBrowserUseSettingsSource>();
            var snapshot = await browser.Settings.LoadAsync(ct).ConfigureAwait(true);
            var changed = snapshot.Values with { AllowDispatchSessions = !snapshot.Values.AllowDispatchSessions };

            await browser.Settings.SaveAsync(changed, ct).ConfigureAwait(true);

            browser.Options.AllowDispatchSessions.ShouldBe(changed.AllowDispatchSessions);
            (await browser.Settings.LoadAsync(ct).ConfigureAwait(true)).Values.AllowDispatchSessions.ShouldBe(changed.AllowDispatchSessions);
            await browser.Gate.WaitIfTakenOverAsync(ct).ConfigureAwait(true);

            await head.DisposeAsync().ConfigureAwait(true);

            Should.Throw<ObjectDisposedException>(() => { _ = browser.Gate.Cancellation; });
            await head.DisposeAsync().ConfigureAwait(true);
        }, ct).ConfigureAwait(true);
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
