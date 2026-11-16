using System.Net;
using System.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Artifacts;

/// <summary>The version PUT applies the configured per-kind byte limit, the version count and the next-number rule before it stores anything.</summary>
public sealed class ArtifactPutLimitsTests
{
    /// <summary>A 300 KiB SVG is over the 256 KiB SVG limit even though it is under the shared text body ceiling.</summary>
    [Fact]
    public async Task SvgOverItsKindLimitIsTooLarge()
    {
        using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata);
        using var client = Client(factory);
        var token = TestContext.Current.CancellationToken;
        var id = "svg-" + Guid.NewGuid().ToString("N");
        using var oversized = ArtifactRequests.Raw(new byte[300 * 1024], "Svg");
        using var refused = await client.PutAsync(Version(id, 1), oversized, token).ConfigureAwait(true);
        refused.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await refused.Content.ReadAsStringAsync(token).ConfigureAwait(true)).Contains("too_large", StringComparison.Ordinal).ShouldBeTrue();
        using var missing = await client.GetAsync(new Uri("/cowork/artifacts/" + id, UriKind.Relative), token).ConfigureAwait(true);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>A later version keeps the kind the artifact was created with, so the request header cannot pick a larger limit.</summary>
    [Fact]
    public async Task AppendedVersionKeepsTheStoredKindLimit()
    {
        using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata);
        using var client = Client(factory);
        var token = TestContext.Current.CancellationToken;
        var id = "svg-" + Guid.NewGuid().ToString("N");
        using var first = ArtifactRequests.Body("<svg/>", "Svg");
        using var created = await client.PutAsync(Version(id, 1), first, token).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var oversized = new byte[300 * 1024];
        oversized[0] = 1;
        using var disguised = ArtifactRequests.Raw(oversized, "Markdown");
        using var refused = await client.PutAsync(Version(id, 2), disguised, token).ConfigureAwait(true);
        refused.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await refused.Content.ReadAsStringAsync(token).ConfigureAwait(true)).Contains("too_large", StringComparison.Ordinal).ShouldBeTrue();
    }

    /// <summary>Small versions stop at the configured version count.</summary>
    [Fact]
    public async Task VersionsStopAtMaxVersions()
    {
        var config = ArtifactTestConfig.Enabled();
        config["Cowork:Artifacts:MaxVersions"] = "3";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var client = Client(factory);
        var token = TestContext.Current.CancellationToken;
        var id = "ver-" + Guid.NewGuid().ToString("N");
        for (var number = 1; number <= 3; number++)
        {
            using var body = ArtifactRequests.Body("v" + number.ToString(System.Globalization.CultureInfo.InvariantCulture), "Markdown");
            using var accepted = await client.PutAsync(Version(id, number), body, token).ConfigureAwait(true);
            accepted.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using var over = ArtifactRequests.Body("v4", "Markdown");
        using var refused = await client.PutAsync(Version(id, 4), over, token).ConfigureAwait(true);
        refused.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await refused.Content.ReadAsStringAsync(token).ConfigureAwait(true)).Contains("quota_exceeded", StringComparison.Ordinal).ShouldBeTrue();
        using var retry = ArtifactRequests.Body("v3", "Markdown");
        using var idempotent = await client.PutAsync(Version(id, 3), retry, token).ConfigureAwait(true);
        idempotent.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Version 3 before version 2 is a conflict, nothing is stored for it, and the artifact stays readable once version 2 and 3 arrive in order.</summary>
    [Fact]
    public async Task OutOfOrderVersionIsAConflictAndHistoryStaysReadable()
    {
        using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata);
        using var client = Client(factory);
        var token = TestContext.Current.CancellationToken;
        var id = "ord-" + Guid.NewGuid().ToString("N");
        using var one = ArtifactRequests.Body("one", "Markdown");
        (await client.PutAsync(Version(id, 1), one, token).ConfigureAwait(true)).StatusCode.ShouldBe(HttpStatusCode.Created);
        using var three = ArtifactRequests.Body("three", "Markdown");
        using var early = await client.PutAsync(Version(id, 3), three, token).ConfigureAwait(true);
        early.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var absent = await client.GetAsync(new Uri("/cowork/artifacts/" + id + "/versions/3/content", UriKind.Relative), token).ConfigureAwait(true);
        absent.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var two = ArtifactRequests.Body("two", "Markdown");
        (await client.PutAsync(Version(id, 2), two, token).ConfigureAwait(true)).StatusCode.ShouldBe(HttpStatusCode.Created);
        using var threeAgain = ArtifactRequests.Body("three", "Markdown");
        (await client.PutAsync(Version(id, 3), threeAgain, token).ConfigureAwait(true)).StatusCode.ShouldBe(HttpStatusCode.Created);
        using var summary = await client.GetAsync(new Uri("/cowork/artifacts/" + id, UriKind.Relative), token).ConfigureAwait(true);
        summary.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static HttpClient Client(ApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        return client;
    }

    private static Uri Version(string id, int number) => new("/cowork/artifacts/" + id + "/versions/" + number.ToString(System.Globalization.CultureInfo.InvariantCulture), UriKind.Relative);
}
