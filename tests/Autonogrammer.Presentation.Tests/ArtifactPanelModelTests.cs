using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Presentation.Cowork.Artifacts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The panel walks the six states, destroys its surface on hide, stop, close and sign-out, and reports every blocked navigation.</summary>
public sealed class ArtifactPanelModelTests
{
    private static readonly string[] ApiOrigins = ["api.example"];
    /// <summary>Show walks Loading then Running, and the fake records each transition.</summary>
    [Fact]
    public async Task ShowWalksLoadingThenRunning()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: true));

        await panel.ShowAsync(Plan(3, 5), CancellationToken.None);

        surface.Transitions.ShouldBe(new[] { ArtifactSurfaceState.Idle, ArtifactSurfaceState.Loading, ArtifactSurfaceState.Running });
        panel.SurfaceState.ShouldBe(ArtifactSurfaceState.Running);
        panel.IsOpen.ShouldBeTrue();
        panel.Heading.ShouldBe("Artifact: Plan, version 3 of 5");
        panel.StatusLine.ShouldBe(ArtifactPanelModel.SandboxStatus);
        surface.Loads.Single().ApprovedOrigins.ShouldBeEmpty();
    }

    /// <summary>Every cancelled navigation is kept and announced; the surface reports NavigationBlocked.</summary>
    [Fact]
    public async Task EveryBlockedNavigationIsReported()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Plan(1, 1), CancellationToken.None);

        surface.SimulateBlockedNavigation(new Uri("https://one.example/"));
        surface.SimulateBlockedNavigation(new Uri("https://two.example/"));

        panel.SurfaceState.ShouldBe(ArtifactSurfaceState.NavigationBlocked);
        panel.BlockedNavigations.ShouldBe(new[] { new Uri("https://one.example/"), new Uri("https://two.example/") });
        panel.Announcement.ShouldBe("Navigation blocked: https://two.example/");
        surface.BlockedUrls.Count.ShouldBe(2);
    }

    /// <summary>Hide destroys the surface and drops the shown version, so nothing is preserved.</summary>
    [Fact]
    public async Task HideDestroysTheSurfaceAndPreservesNothing()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Plan(2, 2), CancellationToken.None);

        panel.Hide();

        surface.DestroyCount.ShouldBe(1);
        surface.Transitions[^1].ShouldBe(ArtifactSurfaceState.Idle);
        panel.IsOpen.ShouldBeFalse();
        panel.Current.ShouldBeNull();
    }

    /// <summary>Stop stops the document, destroys the surface and leaves the panel open on the same version.</summary>
    [Fact]
    public async Task StopStopsThenDestroysAndKeepsThePanelOpen()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Plan(2, 2), CancellationToken.None);

        await panel.StopAsync(CancellationToken.None);

        surface.Transitions.ShouldContain(ArtifactSurfaceState.Stopped);
        surface.Transitions[^1].ShouldBe(ArtifactSurfaceState.Idle);
        surface.DestroyCount.ShouldBe(1);
        panel.IsOpen.ShouldBeTrue();
        panel.Current.ShouldNotBeNull();
    }

    /// <summary>Close and sign-out destroy the surface, clear the blocked report, and sign-out clears every grant.</summary>
    [Fact]
    public async Task CloseAndSignOutDestroyTheSurface()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Plan(1, 1), CancellationToken.None);
        surface.SimulateBlockedNavigation(new Uri("https://x.example/"));

        panel.Close();

        surface.DestroyCount.ShouldBe(1);
        panel.IsOpen.ShouldBeFalse();
        panel.BlockedNavigations.ShouldBeEmpty();

        await panel.ShowAsync(Plan(1, 1), CancellationToken.None);
        panel.Grants.Add(Plan(1, 1).Key, ApiOrigins);
        panel.SignOut();

        surface.DestroyCount.ShouldBe(2);
        panel.Grants.ApprovedFor(Plan(1, 1).Key).ShouldBeEmpty();
        panel.IsOpen.ShouldBeFalse();
    }

    /// <summary>An unavailable surface carries its reason, such as a missing Mermaid bundle.</summary>
    [Fact]
    public void UnavailableCarriesTheReason()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: false));

        surface.SimulateUnavailable(ArtifactSurfaceReason.MermaidAssetMissing);

        panel.SurfaceState.ShouldBe(ArtifactSurfaceState.Unavailable);
        panel.SurfaceReason.ShouldBe(ArtifactSurfaceReason.MermaidAssetMissing);
    }

    /// <summary>The first artifact opens the panel when AutoOpen is set; a later version never moves the shown version.</summary>
    [Fact]
    public async Task AutoOpenOpensOnlyTheFirstArtifactAndNeverMovesFocus()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: true));

        await panel.NotifyVersionCreatedAsync(Plan(1, 1), CancellationToken.None);
        panel.IsOpen.ShouldBeTrue();
        panel.Current!.ArtifactId.ShouldBe("a1");

        await panel.NotifyVersionCreatedAsync(Other(), CancellationToken.None);
        panel.Current!.ArtifactId.ShouldBe("a1");
        panel.Announcement.ShouldBe("Version 1 created by the agent");
        surface.Loads.Count.ShouldBe(1);
    }

    /// <summary>Without AutoOpen the panel stays closed, and the announcement still reports the version.</summary>
    [Fact]
    public async Task WithoutAutoOpenThePanelStaysClosed()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: false));

        await panel.NotifyVersionCreatedAsync(Plan(3, 3), CancellationToken.None);

        panel.IsOpen.ShouldBeFalse();
        panel.Announcement.ShouldBe("Version 3 created by the agent");
        surface.Loads.ShouldBeEmpty();
    }

    /// <summary>The panel chrome carries the fixed ADR copy: the region label and the sandbox status line.</summary>
    [Fact]
    public void ChromeCopyIsFixed()
    {
        ArtifactPanelModel.ChromeLabel.ShouldBe("Generated content, not part of Autonogrammer");
        ArtifactPanelModel.SandboxStatus.ShouldBe("Running in a sandbox. No network.");
    }

    private static ArtifactPanelItem Plan(int number, int count) =>
        new("a1", "Plan", ArtifactKind.Markdown, number, count, new string('a', 64), []);

    private static ArtifactPanelItem Other() =>
        new("a2", "Other", ArtifactKind.Csv, 1, 1, new string('b', 64), []);
}
