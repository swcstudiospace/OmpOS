using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Presentation.Cowork.Artifacts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The Access review shows both host forms, focuses Deny, binds grants to a version, and never approves by waiting.</summary>
public sealed class ArtifactAccessReviewTests
{
    private const string Hash1 = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string Hash2 = "2222222222222222222222222222222222222222222222222222222222222222";
    private static readonly string[] ApiOrigins = ["api.example"];

    /// <summary>The review lists each origin in ASCII and decoded forms, focuses Deny and carries the fixed copy.</summary>
    [Fact]
    public async Task ReviewShowsBothFormsAndFocusesDeny()
    {
        using var panel = new ArtifactPanelModel(new FakeArtifactSurface(), new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Item(1, Hash1, "bücher.example"), CancellationToken.None);
        var clock = new ManualClock();

        var review = panel.CreateAccessReview(clock, TimeSpan.FromMinutes(2));

        review.ShouldNotBeNull();
        ArtifactAccessReviewModel.FocusedChoice.ShouldBe(ArtifactAccessChoice.Deny);
        review.Origins.Single().Ascii.ShouldBe("xn--bcher-kva.example");
        review.Origins.Single().Decoded.ShouldBe("bücher.example");
        review.Origins.Single().MixedScript.ShouldBeFalse();
        ArtifactAccessReviewModel.ReasonLabel.ShouldBe("Written by the agent");
        ArtifactAccessReviewModel.WarningText.ShouldBe("Anything you type into this artifact can be sent to these hosts. Requests are made from your device.");
        ArtifactAccessReviewModel.BannerText.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A label that mixes Cyrillic and Latin letters is marked, and an ordinary host is not.</summary>
    [Fact]
    public void MixedScriptIsMarkedAndOrdinaryHostsAreNot()
    {
        ArtifactHostNames.Describe("раураl.example").MixedScript.ShouldBeTrue();
        ArtifactHostNames.Describe("example.com").MixedScript.ShouldBeFalse();
        ArtifactHostNames.AsciiForm("bücher.example").ShouldBe("xn--bcher-kva.example");
    }

    /// <summary>Escape denies, and nothing is approved for the version.</summary>
    [Fact]
    public async Task EscapeDeniesWithNothingApproved()
    {
        using var panel = new ArtifactPanelModel(new FakeArtifactSurface(), new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Item(1, Hash1, "api.example"), CancellationToken.None);
        var review = panel.CreateAccessReview(new ManualClock(), TimeSpan.FromMinutes(2))!;

        var decision = review.Escape();

        decision.Choice.ShouldBe(ArtifactAccessChoice.Deny);
        decision.Origins.ShouldBeEmpty();
        decision.Reason.ShouldBe("Escape");
    }

    /// <summary>A timeout denies with nothing approved, and the first answer stays final.</summary>
    [Fact]
    public async Task TimeoutDeniesAndTheFirstAnswerIsFinal()
    {
        using var panel = new ArtifactPanelModel(new FakeArtifactSurface(), new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Item(1, Hash1, "api.example"), CancellationToken.None);
        var clock = new ManualClock();
        var review = panel.CreateAccessReview(clock, TimeSpan.FromMinutes(2))!;

        review.ExpireIfDue().ShouldBeNull();
        clock.Advance(TimeSpan.FromMinutes(3));
        var decision = review.ExpireIfDue();

        decision.ShouldNotBeNull();
        decision.Choice.ShouldBe(ArtifactAccessChoice.Deny);
        decision.Origins.ShouldBeEmpty();
        review.Choose(ArtifactAccessChoice.AllowVersion).ShouldBeSameAs(decision);
    }

    /// <summary>Allow for this session approves in memory for the open surface only; hiding the panel drops it.</summary>
    [Fact]
    public async Task AllowForThisSessionIsInMemoryOnly()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Item(1, Hash1, "api.example"), CancellationToken.None);
        var review = panel.CreateAccessReview(new ManualClock(), TimeSpan.FromMinutes(2))!;

        await panel.ApplyAsync(review.Choose(ArtifactAccessChoice.AllowSession), CancellationToken.None);

        panel.ApprovedOrigins.ShouldBe(ApiOrigins);
        surface.Loads[^1].ApprovedOrigins.ShouldBe(ApiOrigins);
        panel.StatusLine.ShouldBe(ArtifactPanelModel.ApprovedStatus);
        panel.Grants.ApprovedFor(review.Version).ShouldBeEmpty();

        panel.Hide();
        await panel.ShowAsync(Item(1, Hash1, "api.example"), CancellationToken.None);
        panel.ApprovedOrigins.ShouldBeEmpty();
    }

    /// <summary>Allow for this version binds to ArtifactId, Number and ContentSha256; a new version asks again.</summary>
    [Fact]
    public async Task AllowForThisVersionBindsToNumberAndHash()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Item(1, Hash1, "api.example"), CancellationToken.None);
        var review = panel.CreateAccessReview(new ManualClock(), TimeSpan.FromMinutes(2))!;

        await panel.ApplyAsync(review.Choose(ArtifactAccessChoice.AllowVersion), CancellationToken.None);

        panel.Grants.ApprovedFor(new ArtifactVersionKey("a1", 1, Hash1)).ShouldBe(ApiOrigins);
        panel.ApprovedOrigins.ShouldBe(ApiOrigins);

        await panel.ShowAsync(Item(2, Hash2, "api.example"), CancellationToken.None);
        panel.ApprovedOrigins.ShouldBeEmpty();
        panel.ReviewAccessAvailable.ShouldBeTrue();

        await panel.ShowAsync(Item(1, Hash2, "api.example"), CancellationToken.None);
        panel.ApprovedOrigins.ShouldBeEmpty();
    }

    /// <summary>Deny approves nothing and does not rebuild the surface.</summary>
    [Fact]
    public async Task DenyApprovesNothing()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Item(1, Hash1, "api.example"), CancellationToken.None);
        var review = panel.CreateAccessReview(new ManualClock(), TimeSpan.FromMinutes(2))!;

        await panel.ApplyAsync(review.Choose(ArtifactAccessChoice.Deny), CancellationToken.None);

        panel.ApprovedOrigins.ShouldBeEmpty();
        surface.Loads.Count.ShouldBe(1);
        surface.DestroyCount.ShouldBe(0);
    }

    /// <summary>Access revokes the grant and rebuilds the surface with no network.</summary>
    [Fact]
    public async Task RevokeRebuildsTheSurfaceWithNoNetwork()
    {
        var surface = new FakeArtifactSurface();
        using var panel = new ArtifactPanelModel(surface, new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Item(1, Hash1, "api.example"), CancellationToken.None);
        var review = panel.CreateAccessReview(new ManualClock(), TimeSpan.FromMinutes(2))!;
        await panel.ApplyAsync(review.Choose(ArtifactAccessChoice.AllowVersion), CancellationToken.None);

        await panel.RevokeAccessAsync(CancellationToken.None);

        panel.Grants.ApprovedFor(review.Version).ShouldBeEmpty();
        surface.Loads[^1].ApprovedOrigins.ShouldBeEmpty();
        surface.DestroyCount.ShouldBe(2);
    }

    /// <summary>A storage request is answered and never granted.</summary>
    [Fact]
    public void StorageRequestsAreAnsweredNotGranted()
    {
        ArtifactAccessReviewModel.AnswerPermission("storage").ShouldBe("This artifact asked to store data; storage is not available");
        ArtifactAccessReviewModel.AnswerPermission("network").ShouldBeNull();
    }

    /// <summary>A version that declares no origin has no review.</summary>
    [Fact]
    public async Task NoOriginMeansNoReview()
    {
        using var panel = new ArtifactPanelModel(new FakeArtifactSurface(), new ArtifactPanelOptions(AutoOpen: false));
        await panel.ShowAsync(Item(1, Hash1), CancellationToken.None);

        panel.CreateAccessReview(new ManualClock(), TimeSpan.FromMinutes(2)).ShouldBeNull();
        panel.ReviewAccessAvailable.ShouldBeFalse();
    }

    private static ArtifactPanelItem Item(int number, string hash, params string[] origins) =>
        new("a1", "Plan", ArtifactKind.Html, number, number, hash, origins);

    /// <summary>A clock the test moves by hand.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => _now;

        /// <summary>Moves the clock forward.</summary>
        /// <param name="delta">The distance to move.</param>
        public void Advance(TimeSpan delta) => _now += delta;
    }
}
