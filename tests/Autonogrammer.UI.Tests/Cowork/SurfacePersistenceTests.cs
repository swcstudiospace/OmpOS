using Autonogrammer.UI.Components.Cowork;
using Autonogrammer.UI.Components.Shell;
using Autonogrammer.UI.Tests.Fakes;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Cowork;

/// <summary>A stored surface restores only when Cowork is available, and a switch writes it.</summary>
public sealed class SurfacePersistenceTests : UiTestContext
{
    /// <summary>A stored cowork restores when Cowork is available, and a visit does not write.</summary>
    [Fact]
    public void AStoredCoworkRestoresWhenAvailable()
    {
        JSInterop.Setup<string?>("autonogrammerSurface.read").SetResult("cowork");
        Services.AddSingleton<ICoworkAccess>(new AvailableAccess());
        using var harness = PanelHarness.With(("a", Autonogrammer.Contracts.Agents.SessionLifecycle.Running, "Alpha"));

        var cut = RenderView(harness);

        cut.WaitForAssertion(() => Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBe("cowork"));
        JSInterop.Invocations.ShouldNotContain(call => call.Identifier == "autonogrammerSurface.write");
    }

    [Fact]
    public void AnUnavailableStoredCoworkStaysOnCode()
    {
        JSInterop.Setup<string?>("autonogrammerSurface.read").SetResult("cowork");
        Services.AddSingleton<ICoworkAccess>(new UnavailableAccess());
        using var harness = PanelHarness.With(("a", Autonogrammer.Contracts.Agents.SessionLifecycle.Running, "Alpha"));

        var cut = RenderView(harness);

        cut.WaitForAssertion(() => JSInterop.Invocations.ShouldContain(call => call.Identifier == "autonogrammerSurface.paint" && Equals(call.Arguments[0], "code")));
        Navigation.ToBaseRelativePath(Navigation.Uri).ShouldNotBe("cowork", StringComparer.Ordinal);
    }

    /// <summary>Choosing Cowork writes the surface. C# and Razor name no browser storage API.</summary>
    [Fact]
    public async Task ASwitchWritesTheSurfaceAndSourceNamesNoStorageApi()
    {
        JSInterop.Setup<string?>("autonogrammerSurface.read").SetResult("code");
        Services.AddSingleton<ICoworkAccess>(new AvailableAccess());
        using var harness = PanelHarness.With(("a", Autonogrammer.Contracts.Agents.SessionLifecycle.Running, "Alpha"));
        var cut = RenderView(harness);
        await cut.WaitForStateAsync(() => cut.FindAll("#" + SurfaceTabs.CoworkTabId).Count == 1).ConfigureAwait(true);

        await cut.Find("#" + SurfaceTabs.CoworkTabId).ClickAsync().ConfigureAwait(true);

        JSInterop.Invocations.ShouldContain(call => call.Identifier == "autonogrammerSurface.write" && Equals(call.Arguments[0], "cowork"));
        var root = FindRepoRoot();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "Autonogrammer.UI"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".razor", StringComparison.Ordinal)))
        {
            var text = await File.ReadAllTextAsync(file, Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
            text.ShouldNotContain("localStorage");
            text.ShouldNotContain("sessionStorage");
        }
    }

    /// <summary>Reduced motion turns the dock transition off.</summary>
    [Fact]
    public async Task ReducedMotionTurnsTransitionsOff()
    {
        var css = await File.ReadAllTextAsync(Path.Combine(FindRepoRoot(), "src", "Autonogrammer.UI", "wwwroot", "styles", "surface.css"), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        css.ShouldContain("@media (prefers-reduced-motion: reduce)");
        css.ShouldContain("transition: none");
    }

    /// <summary>The shortcut host's code and cowork identifiers switch the surface.</summary>
    [Fact]
    public async Task ShortcutsSwitchTheSurface()
    {
        Services.AddSingleton<ICoworkAccess>(new AvailableAccess());
        using var harness = PanelHarness.With(("a", Autonogrammer.Contracts.Agents.SessionLifecycle.Running, "Alpha"));
        var cut = RenderView(harness);
        var host = cut.FindComponent<ShortcutHost>();

        await cut.InvokeAsync(() => host.Instance.HandleShortcutAsync(ShortcutHost.Cowork)).ConfigureAwait(true);

        Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBe("cowork");

        await cut.InvokeAsync(() => host.Instance.HandleShortcutAsync(ShortcutHost.Code)).ConfigureAwait(true);

        Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBeEmpty();
    }

    private Microsoft.AspNetCore.Components.NavigationManager Navigation => Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

    private IRenderedComponent<WorkbenchView> RenderView(PanelHarness harness)
    {
        var settings = new Autonogrammer.Presentation.Settings.SettingsWindowModel([new StubSettingsPage()]);
        return Render<WorkbenchView>(p => p
            .Add(c => c.Panel, harness.Panel)
            .Add(c => c.Board, harness.Board)
            .Add(c => c.Settings, settings));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Autonogrammer.slnx")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    private sealed class AvailableAccess : ICoworkAccess
    {
        public event EventHandler? Changed { add { } remove { } }

        public bool IsResolved => true;

        public bool IsAvailable => true;

        public bool OffersSignIn => false;

        public bool LoadFailed => false;

        public Task RetryAsync() => Task.CompletedTask;
    }

    private sealed class UnavailableAccess : ICoworkAccess
    {
        public event EventHandler? Changed { add { } remove { } }

        public bool IsResolved => true;

        public bool IsAvailable => false;

        public bool OffersSignIn => true;

        public bool LoadFailed => false;

        public Task RetryAsync() => Task.CompletedTask;
    }
}
