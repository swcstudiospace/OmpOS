using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Autonogrammer.Api.Cowork.Artifacts;
using Autonogrammer.Api.IntegrationTests.Security;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Artifacts;

/// <summary>Render tickets, render headers, anonymous access and path redaction.</summary>
public sealed class ArtifactRenderTicketTests
{
    /// <summary>Verifies expiry, another version, a tampered segment, and the render document headers.</summary>
    [Fact]
    public async Task TicketsExpireAndRenderKeepsItsOwnHeaders()
    {
        using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        var token = TestContext.Current.CancellationToken;
        var id = "art-" + Guid.NewGuid().ToString("N");
        var url = await IssueAsync(client, id, token).ConfigureAwait(true);
        using var rendered = await client.GetAsync(new Uri(url, UriKind.Relative), token).ConfigureAwait(true);
        rendered.StatusCode.ShouldBe(HttpStatusCode.OK);
        rendered.Headers.Contains("X-Frame-Options").ShouldBeFalse();
        rendered.Headers.GetValues("Content-Security-Policy").Single().Contains("frame-ancestors 'none'", StringComparison.Ordinal).ShouldBeTrue();
        rendered.Headers.GetValues("Cache-Control").Any(static value => string.Equals(value, "no-store", StringComparison.Ordinal)).ShouldBeTrue();
        rendered.Headers.GetValues("Permissions-Policy").Single().Contains("clipboard-read=()", StringComparison.Ordinal).ShouldBeTrue();
        var tickets = factory.Services.GetRequiredService<ArtifactRenderTickets>();
        using var old = await client.GetAsync(new Uri("/cowork/artifact-render/" + tickets.Issue("alice", id, 1, "abc", DateTimeOffset.UtcNow.AddSeconds(-5)), UriKind.Relative), token).ConfigureAwait(true);
        old.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var wrongVersion = await client.GetAsync(new Uri("/cowork/artifact-render/" + tickets.Issue("alice", id, 2, "abc"), UriKind.Relative), token).ConfigureAwait(true);
        wrongVersion.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var tampered = url + "b";
        using var bad = await client.GetAsync(new Uri(tampered, UriKind.Relative), token).ConfigureAwait(true);
        bad.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Verifies anonymous render under OIDC and the subject allow-list.</summary>
    [Fact]
    public async Task AnonymousRenderAndSubjectAllowList()
    {
        var oidc = ArtifactTestConfig.Enabled();
        oidc["Api:Auth:Mode"] = "Oidc";
        oidc["Api:Auth:Authority"] = JwtTestHost.Issuer;
        oidc["Api:Auth:AllowDevelopmentPrincipal"] = "false";
        using var oidcFactory = new ApiFactory(oidc, JwtTestHost.InjectStaticMetadata);
        using var owner = oidcFactory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        var token = TestContext.Current.CancellationToken;
        var url = await IssueAsync(owner, "oidc-" + Guid.NewGuid().ToString("N"), token).ConfigureAwait(true);
        using var anonymous = oidcFactory.CreateClient();
        using var rendered = await anonymous.GetAsync(new Uri(url, UriKind.Relative), token).ConfigureAwait(true);
        rendered.StatusCode.ShouldBe(HttpStatusCode.OK);
        var allow = ArtifactTestConfig.Enabled();
        allow["Api:Auth:AllowedSubjects:0"] = "alice";
        using var limited = new ApiFactory(allow, JwtTestHost.InjectStaticMetadata);
        using var mallory = limited.CreateClient();
        mallory.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("mallory"));
        using var forbidden = await mallory.GetAsync(new Uri("/cowork/artifacts", UriKind.Relative), token).ConfigureAwait(true);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await forbidden.Content.ReadAsStringAsync(token).ConfigureAwait(true)).Contains("subject_not_allowed", StringComparison.Ordinal).ShouldBeTrue();
    }

    /// <summary>Verifies anonymous render under owner-token mode.</summary>
    [Fact]
    public async Task OwnerTokenRenderNeedsNoSubject()
    {
        var ownerConfig = OwnerTokenTestHost.Config();
        var enabled = ArtifactTestConfig.Enabled();
        ownerConfig["Cowork:Artifacts:Enabled"] = "true";
        ownerConfig["Cowork:Artifacts:RenderTicketKeys:0"] = enabled["Cowork:Artifacts:RenderTicketKeys:0"];
        ownerConfig["Cowork:Artifacts:RenderTicketKeys:1"] = enabled["Cowork:Artifacts:RenderTicketKeys:1"];
        using var ownerFactory = OwnerTokenTestHost.CreateFactory(ownerConfig);
        using var signed = ownerFactory.CreateClient();
        OwnerTokenTestHost.UseBearer(signed, OwnerTokenTestHost.Token);
        var token = TestContext.Current.CancellationToken;
        var url = await IssueAsync(signed, "own-" + Guid.NewGuid().ToString("N"), token).ConfigureAwait(true);
        using var bare = ownerFactory.CreateClient();
        using var rendered = await bare.GetAsync(new Uri(url, UriKind.Relative), token).ConfigureAwait(true);
        rendered.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies a render activity and module log do not contain the ticket.</summary>
    [Fact]
    public async Task RenderPathIsRedacted()
    {
        var seen = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static _ => true,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = seen.Add,
        };
        ActivitySource.AddActivityListener(listener);
        using var logs = new CapturingLoggerProvider();
        using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata, logs);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        var token = TestContext.Current.CancellationToken;
        var url = await IssueAsync(client, "redact-" + Guid.NewGuid().ToString("N"), token).ConfigureAwait(true);
        var ticket = url["/cowork/artifact-render/".Length..];
        using var rendered = await client.GetAsync(new Uri(url, UriKind.Relative), token).ConfigureAwait(true);
        rendered.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var activity in seen)
        {
            foreach (var tag in activity.Tags)
            {
                (tag.Value ?? string.Empty).Contains(ticket, StringComparison.Ordinal).ShouldBeFalse();
            }
        }

        foreach (var entry in logs.Entries)
        {
            if (entry.Category.Contains("Cowork.Artifacts", StringComparison.Ordinal))
            {
                entry.Message.Contains(ticket, StringComparison.Ordinal).ShouldBeFalse();
            }
        }
    }

    private static async Task<string> IssueAsync(HttpClient client, string id, CancellationToken token)
    {
        using var body = ArtifactRequests.Body("<p>hi</p>", "Html");
        using var created = await client.PutAsync(new Uri("/cowork/artifacts/" + id + "/versions/1", UriKind.Relative), body, token).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var empty = new StringContent(string.Empty);
        using var issued = await client.PostAsync(new Uri("/cowork/artifacts/" + id + "/versions/1/render-tickets", UriKind.Relative), empty, token).ConfigureAwait(true);
        return JsonDocument.Parse(await issued.Content.ReadAsStringAsync(token).ConfigureAwait(true)).RootElement.GetProperty("url").GetString()!;
    }
}
