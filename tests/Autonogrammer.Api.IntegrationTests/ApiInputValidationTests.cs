using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.InMemory;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Inbound input validation: correlation id shape and bound (F-05), resume positions (F-06),
/// harness subtypes without a typed payload (F-07), and the owner id shape and bound (F-09).
/// </summary>
public sealed class ApiInputValidationTests
{
    private const string CorrelationHeader = "x-correlation-id";

    /// <summary>Verifies an inbound id made of allowed characters within the bound is echoed unchanged.</summary>
    [Theory]
    [InlineData("a")]
    [InlineData("trace_ID-42")]
    [InlineData("3f2b8c1e-9a4d-4e7f-8b2c-1d0e9f8a7b6c")]
    public async Task CorrelationId_AllowedShape_IsEchoed(string inbound)
    {
        var echoed = await EchoedCorrelationIdAsync(inbound, TestContext.Current.CancellationToken).ConfigureAwait(true);

        echoed.ShouldBe(inbound);
    }

    /// <summary>Verifies a 64-character inbound id, the upper boundary, is echoed unchanged.</summary>
    [Fact]
    public async Task CorrelationId_SixtyFourCharacters_IsEchoed()
    {
        var boundary = new string('c', 64);

        var echoed = await EchoedCorrelationIdAsync(boundary, TestContext.Current.CancellationToken).ConfigureAwait(true);

        echoed.ShouldBe(boundary);
    }

    /// <summary>Verifies a 65-character inbound id is replaced by a minted guid, never echoed.</summary>
    [Fact]
    public async Task CorrelationId_SixtyFiveCharacters_IsReplacedByMintedId()
    {
        var oversized = new string('c', 65);

        var echoed = await EchoedCorrelationIdAsync(oversized, TestContext.Current.CancellationToken).ConfigureAwait(true);

        string.Equals(echoed, oversized, StringComparison.Ordinal).ShouldBeFalse("a rejected inbound id must not be echoed");
        Guid.TryParse(echoed, out _).ShouldBeTrue("a rejected inbound id must be replaced with a minted guid");
    }

    /// <summary>Verifies inbound ids with characters outside the allowed set are replaced by a minted guid.</summary>
    [Theory]
    [InlineData("bad!id")]
    [InlineData("trace.id")]
    [InlineData("{3f2b8c1e-9a4d-4e7f-8b2c-1d0e9f8a7b6c}")]
    public async Task CorrelationId_OutsideAllowedShape_IsReplacedByMintedId(string inbound)
    {
        var echoed = await EchoedCorrelationIdAsync(inbound, TestContext.Current.CancellationToken).ConfigureAwait(true);

        string.Equals(echoed, inbound, StringComparison.Ordinal).ShouldBeFalse("a rejected inbound id must not be echoed");
        Guid.TryParse(echoed, out _).ShouldBeTrue("a rejected inbound id must be replaced with a minted guid");
    }

    /// <summary>Verifies valid resume positions from the query or Last-Event-ID header start after that Seq, with Last-Event-ID taking precedence.</summary>
    [Theory]
    [InlineData("?since=0", null, "1")]
    [InlineData("", "0", "1")]
    [InlineData("?since=1", "4", "5")]
    public async Task Events_ValidResumePosition_StreamsAfterThatSeq(string query, string? lastEventId, string expectedFirstId)
    {
        using var factory = ScriptedFactory(ScriptedHarnessFactory.DeltaScript(11));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await AcceptPromptAsync(client, testToken).ConfigureAwait(true);

        var items = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events{query}", 1, testToken, lastEventId).ConfigureAwait(true);

        items.Count.ShouldBe(1);
        items[0].EventId.ShouldBe(expectedFirstId);
    }

    /// <summary>Verifies non-negative 64-bit integers are the only resume positions accepted; signs, whitespace, decimals, hex, and overflow return 400.</summary>
    [Theory]
    [InlineData("?since=%2B1", null)]
    [InlineData("?since=1%20", null)]
    [InlineData("?since=1.5", null)]
    [InlineData("?since=0x10", null)]
    [InlineData("?since=9223372036854775808", null)]
    [InlineData("", "+1")]
    [InlineData("", "abc")]
    [InlineData("?since=-1", "4")]
    [InlineData("?since=1", "9223372036854775808")]
    public async Task Events_MalformedResumePosition_Returns400Problem(string query, string? lastEventId)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/sessions/{id}/events{query}", UriKind.Relative));
        if (lastEventId is not null)
        {
            request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);
        }

        using var response = await client.SendAsync(request, testToken).ConfigureAwait(true);

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    /// <summary>Verifies a harness subtype without a typed payload degrades to an unknown payload and the stream still runs to its terminal event.</summary>
    [Fact]
    public async Task Events_HarnessSubtypeWithoutTypedPayload_StreamContinuesToCompletion()
    {
        var script = new InMemoryScript
        {
            Steps =
            [
                new EmitStep(new ThoughtEvent("considering")),
                new EmitStep(new ToolCallEvent("call-1", "search", "{}")),
                new EmitStep(new PlanEvent("# plan")),
                new EmitStep(new CompletedEvent("end_turn")),
            ],
        };
        using var factory = ScriptedFactory(script);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await AcceptPromptAsync(client, testToken).ConfigureAwait(true);

        var items = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 50, testToken).ConfigureAwait(true);

        items.Count.ShouldBeGreaterThanOrEqualTo(5);
        items[^1].EventType.ShouldBe("completed");
        var seqs = items.Select(static item => long.Parse(item.EventId!, CultureInfo.InvariantCulture)).ToList();
        seqs.ShouldBe(Enumerable.Range(1, seqs.Count).Select(static i => (long)i).ToList());
    }

    /// <summary>Verifies a subject made of the owner id character set is persisted as the session owner exactly as the token carries it.</summary>
    [Fact]
    public async Task Create_SubjectWithOwnerCharset_IsPersistedAsOwner()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        const string Subject = "ops.team_lead@example-1";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken(Subject));

        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);

        session.OwnerId.ShouldBe(Subject);
    }

    /// <summary>Verifies a body owner id of exactly 128 characters is refused with 400: the owner is never taken from the body.</summary>
    [Fact]
    public async Task Create_OwnerIdAt128Characters_IsRefused()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await PostSessionAsync(client, $"{{\"ownerId\":\"{new string('o', 128)}\"}}", testToken).ConfigureAwait(true);

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    /// <summary>Verifies an owner id one character over the bound returns 400 with a problem body.</summary>
    [Fact]
    public async Task Create_OwnerIdAt129Characters_Returns400Problem()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await PostSessionAsync(client, $"{{\"ownerId\":\"{new string('o', 129)}\"}}", testToken).ConfigureAwait(true);

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    /// <summary>Verifies owner ids with characters outside the allowed set, or empty, return 400 with a problem body.</summary>
    [Theory]
    [InlineData("bad owner")]
    [InlineData("owner/evil")]
    [InlineData("owner\u00e9")]
    [InlineData("")]
    public async Task Create_OwnerIdOutsideCharset_Returns400Problem(string owner)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await PostSessionAsync(client, $"{{\"ownerId\":\"{owner}\"}}", testToken).ConfigureAwait(true);

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    private static ApiFactory ScriptedFactory(InMemoryScript script) =>
        new(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script)));

    private static async Task<string> AcceptPromptAsync(HttpClient client, CancellationToken testToken)
    {
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(false);
        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"stream me\"}", testToken).ConfigureAwait(false);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return id;
    }

    private static async Task<string> EchoedCorrelationIdAsync(string inbound, CancellationToken testToken)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health/live", UriKind.Relative));
        request.Headers.TryAddWithoutValidation(CorrelationHeader, inbound);
        using var response = await client.SendAsync(request, testToken).ConfigureAwait(false);
        return string.Join(",", response.Headers.GetValues(CorrelationHeader));
    }

    private static void AssertProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.ShouldBe(expected);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    private static async Task<HttpResponseMessage> PostSessionAsync(HttpClient client, string json, CancellationToken testToken)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri("/sessions", UriKind.Relative), content, testToken).ConfigureAwait(false);
    }
}
