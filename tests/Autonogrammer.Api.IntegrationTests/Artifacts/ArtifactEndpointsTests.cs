using System.Net;
using System.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Artifacts;

/// <summary>Owner 404 parity, attachment bytes, body ceilings and the disabled default.</summary>
public sealed class ArtifactEndpointsTests
{
    /// <summary>Verifies another owner's id matches the absent-id 404.</summary>
    [Fact]
    public async Task OtherOwnerMatchesAbsentNotFound()
    {
        using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata);
        using var alice = factory.CreateClient();
        using var bob = factory.CreateClient();
        alice.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        bob.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("bob"));
        var token = TestContext.Current.CancellationToken;
        var id = "art-" + Guid.NewGuid().ToString("N");
        using var body = ArtifactRequests.Body("hello", "Markdown");
        using var created = await alice.PutAsync(Version(id, 1), body, token).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var missing = await alice.GetAsync(new Uri("/cowork/artifacts/missing-id", UriKind.Relative), token).ConfigureAwait(true);
        using var foreign = await bob.GetAsync(new Uri("/cowork/artifacts/" + id, UriKind.Relative), token).ConfigureAwait(true);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await missing.Content.ReadAsStringAsync(token).ConfigureAwait(true)).Contains("not_found", StringComparison.Ordinal).ShouldBeTrue();
        var foreignBody = await foreign.Content.ReadAsStringAsync(token).ConfigureAwait(true);
        foreignBody.Contains("not_found", StringComparison.Ordinal).ShouldBeTrue();
        foreignBody.Contains("alice", StringComparison.Ordinal).ShouldBeFalse();
    }

    /// <summary>Verifies content is an attachment octet-stream with nosniff.</summary>
    [Fact]
    public async Task ContentIsAttachmentOctetStream()
    {
        using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        var token = TestContext.Current.CancellationToken;
        var id = "art-" + Guid.NewGuid().ToString("N");
        using var body = ArtifactRequests.Body("hello", "Markdown");
        using var created = await client.PutAsync(Version(id, 1), body, token).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var content = await client.GetAsync(new Uri("/cowork/artifacts/" + id + "/versions/1/content", UriKind.Relative), token).ConfigureAwait(true);
        content.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.Content.Headers.ContentType!.MediaType.ShouldBe("application/octet-stream");
        content.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        content.Headers.GetValues("X-Content-Type-Options").Any(static value => string.Equals(value, "nosniff", StringComparison.Ordinal)).ShouldBeTrue();
        content.Headers.GetValues("X-Frame-Options").Any(static value => string.Equals(value, "DENY", StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>Verifies a disabled host starts and answers cowork_disabled.</summary>
    [Fact]
    public async Task DisabledHostAnswersCoworkDisabled()
    {
        using var disabled = new ApiFactory(JwtTestHost.DevelopmentConfig(), JwtTestHost.InjectStaticMetadata);
        using var off = disabled.CreateClient();
        using var denied = await off.GetAsync(new Uri("/cowork/artifacts", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        denied.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).Contains("cowork_disabled", StringComparison.Ordinal).ShouldBeTrue();
    }

    /// <summary>Verifies image and text body ceilings and the account quota.</summary>
    [Fact]
    public async Task BodyCeilingsAndQuota()
    {
        var config = ArtifactTestConfig.Enabled();
        config["Cowork:Artifacts:MaxArtifacts"] = "1";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        var token = TestContext.Current.CancellationToken;
        var image = new byte[(int)(5.5 * 1024 * 1024)];
        image[0] = 1;
        using var acceptedBody = ArtifactRequests.Raw(image, "Image");
        using var accepted = await client.PutAsync(Version("img-" + Guid.NewGuid().ToString("N"), 1), acceptedBody, token).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Created, await accepted.Content.ReadAsStringAsync(token).ConfigureAwait(true));
        using var overImage = ArtifactRequests.Raw(new byte[(6 * 1024 * 1024) + 1], "Image");
        using var imageRefusal = await client.PutAsync(Version("img2-" + Guid.NewGuid().ToString("N"), 1), overImage, token).ConfigureAwait(true);
        (await imageRefusal.Content.ReadAsStringAsync(token).ConfigureAwait(true)).Contains("too_large", StringComparison.Ordinal).ShouldBeTrue();
        using var overText = ArtifactRequests.Raw(new byte[(1024 * 1024) + 1], "Markdown");
        using var textRefusal = await client.PutAsync(Version("txt-" + Guid.NewGuid().ToString("N"), 1), overText, token).ConfigureAwait(true);
        (await textRefusal.Content.ReadAsStringAsync(token).ConfigureAwait(true)).Contains("too_large", StringComparison.Ordinal).ShouldBeTrue();
        using var second = ArtifactRequests.Body("x", "Markdown");
        using var quota = await client.PutAsync(Version("second-" + Guid.NewGuid().ToString("N"), 1), second, token).ConfigureAwait(true);
        (await quota.Content.ReadAsStringAsync(token).ConfigureAwait(true)).Contains("quota_exceeded", StringComparison.Ordinal).ShouldBeTrue();
    }

    private static Uri Version(string id, int number) => new("/cowork/artifacts/" + id + "/versions/" + number.ToString(System.Globalization.CultureInfo.InvariantCulture), UriKind.Relative);
}
