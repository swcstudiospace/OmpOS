using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Autonogrammer.Web.Services;
using Autonogrammer.Web.Services.Artifacts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>
/// The web artifact client over a scripted API: a render ticket becomes an absolute URL under the API origin and nowhere else, a fresh
/// ticket is reused and an expired one is replaced once, and each refusal is named as the UI names it (sign in, sync is off, rate limited).
/// </summary>
public sealed class ArtifactRenderRouteTests
{
    private const string ApiOrigin = "https://api.example.invalid/";
    private const string ArtifactId = "artifact-1";

    /// <summary>A ticket becomes an absolute URL on the API origin, and the call is a single POST.</summary>
    [Fact]
    public async Task ATicketBecomesAnAbsoluteUrlUnderTheApiOrigin()
    {
        using var handler = new ScriptedHandler();
        using var response = Json("{\"url\":\"/cowork/artifact-render/abc.def\"}");
        handler.Enqueue(response);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiOrigin, UriKind.Absolute) };
        using var api = new ApiHttp(httpClient);

        var call = await new ArtifactWebClient(api, new ManualTimeProvider()).RenderUrlAsync(ArtifactId, 1, TestContext.Current.CancellationToken);

        call.State.ShouldBe(ArtifactCallState.Ok);
        call.Value.ShouldNotBeNull().ToString().ShouldBe("https://api.example.invalid/cowork/artifact-render/abc.def");
        handler.Requests.Single().Method.ShouldBe(HttpMethod.Post);
        handler.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/cowork/artifacts/artifact-1/versions/1/render-tickets");
    }

    /// <summary>A ticket on another origin is refused, so no other document is ever framed.</summary>
    [Fact]
    public async Task ATicketOnAnotherOriginIsRefused()
    {
        await AssertTicketRefusedAsync(new Uri("https://evil.example.invalid/cowork/artifact-render/abc.def", UriKind.Absolute));
    }

    /// <summary>A ticket outside the render route of the API origin is refused, so no other document is ever framed.</summary>
    [Fact]
    public async Task ATicketOutsideTheRenderRouteIsRefused()
    {
        await AssertTicketRefusedAsync(new Uri("/cowork/artifacts/artifact-1/content", UriKind.Relative));
    }

    /// <summary>A ticket that carries no URL is refused.</summary>
    [Fact]
    public async Task AnEmptyTicketIsRefused()
    {
        await AssertTicketRefusedAsync(null);
    }

    /// <summary>A fresh ticket is reused; after its reuse window a single new ticket replaces it.</summary>
    [Fact]
    public async Task AFreshTicketIsReusedAndAnExpiredOneIsReplacedOnce()
    {
        using var handler = new ScriptedHandler();
        using var first = Json("{\"url\":\"/cowork/artifact-render/first\"}");
        using var second = Json("{\"url\":\"/cowork/artifact-render/second\"}");
        handler.Enqueue(first);
        handler.Enqueue(second);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiOrigin, UriKind.Absolute) };
        using var api = new ApiHttp(httpClient);
        var time = new ManualTimeProvider();
        var client = new ArtifactWebClient(api, time);
        var ct = TestContext.Current.CancellationToken;

        var firstCall = await client.RenderUrlAsync(ArtifactId, 1, ct);
        var again = await client.RenderUrlAsync(ArtifactId, 1, ct);
        handler.Requests.Count.ShouldBe(1, "a fresh ticket is reused without a request");
        again.Value.ShouldBe(firstCall.Value);

        time.Advance(ArtifactWebClient.TicketReuse + TimeSpan.FromSeconds(1));
        var replaced = await client.RenderUrlAsync(ArtifactId, 1, ct);

        handler.Requests.Count.ShouldBe(2, "an expired ticket is replaced by one new request");
        replaced.Value!.AbsolutePath.ShouldBe("/cowork/artifact-render/second");
    }

    /// <summary>A refused bearer is named as sign-in required.</summary>
    [Fact]
    public async Task AnUnauthorizedAnswerAsksForSignIn()
    {
        using var handler = new ScriptedHandler();
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        handler.Enqueue(response);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiOrigin, UriKind.Absolute) };
        using var api = new ApiHttp(httpClient);

        var call = await new ArtifactWebClient(api, new ManualTimeProvider()).ListAsync(TestContext.Current.CancellationToken);

        call.State.ShouldBe(ArtifactCallState.SignInRequired);
    }

    /// <summary>A rate-limited answer keeps the delay the API asked for.</summary>
    [Fact]
    public async Task ATooManyRequestsAnswerKeepsTheRetryDelay()
    {
        using var handler = new ScriptedHandler();
        using var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        handler.Enqueue(response);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiOrigin, UriKind.Absolute) };
        using var api = new ApiHttp(httpClient);

        var call = await new ArtifactWebClient(api, new ManualTimeProvider()).ListAsync(TestContext.Current.CancellationToken);

        call.State.ShouldBe(ArtifactCallState.RateLimited);
        call.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
    }

    /// <summary>A version with no stored content answers 404, which the UI names as sync is off.</summary>
    [Fact]
    public async Task AVersionWithoutStoredContentIsSyncOff()
    {
        using var handler = new ScriptedHandler();
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        handler.Enqueue(response);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiOrigin, UriKind.Absolute) };
        using var api = new ApiHttp(httpClient);

        var call = await new ArtifactWebClient(api, new ManualTimeProvider()).GetContentAsync(ArtifactId, 1, TestContext.Current.CancellationToken);

        call.State.ShouldBe(ArtifactCallState.SyncOff);
        call.Value.ShouldBeNull();
    }

    /// <summary>Stored content is returned as the bytes it is.</summary>
    [Fact]
    public async Task StoredContentIsReturnedAsBytes()
    {
        using var handler = new ScriptedHandler();
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        handler.Enqueue(response);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiOrigin, UriKind.Absolute) };
        using var api = new ApiHttp(httpClient);

        var call = await new ArtifactWebClient(api, new ManualTimeProvider()).GetContentAsync(ArtifactId, 1, TestContext.Current.CancellationToken);

        call.State.ShouldBe(ArtifactCallState.Ok);
        call.Value.ShouldBe(new byte[] { 1, 2, 3 });
    }

    /// <summary>Asserts that the API's ticket is refused: the call fails and yields no URL. A null ticket is the empty ticket.</summary>
    private static async Task AssertTicketRefusedAsync(Uri? url)
    {
        using var handler = new ScriptedHandler();
        using var response = Json("{\"url\":" + (url is null ? "null" : "\"" + url.OriginalString + "\"") + "}");
        handler.Enqueue(response);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiOrigin, UriKind.Absolute) };
        using var api = new ApiHttp(httpClient);

        var call = await new ArtifactWebClient(api, new ManualTimeProvider()).RenderUrlAsync(ArtifactId, 1, TestContext.Current.CancellationToken).ConfigureAwait(false);

        call.State.ShouldBe(ArtifactCallState.Failed);
        call.Value.ShouldBeNull();
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
