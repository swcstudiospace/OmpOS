using System.Net;
using System.Text;
using System.Text.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Sessions;
using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>Session-endpoint client tests over a scripted handler.</summary>
public sealed class ApiSessionClientTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);

    /// <summary>Create names no owner in its body: the API takes the owner from the authenticated caller.</summary>
    [Fact]
    public async Task CreateSessionSendsNoOwnerInTheBody()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var client = new ApiSessionClient(http);
        using var queued = JsonResponse(HttpStatusCode.Created, SampleSession());
        handler.Enqueue(queued);

        var session = await client.CreateSessionAsync(ct).ConfigureAwait(true);

        session.Id.ShouldBe("s-1");
        handler.Requests.Count.ShouldBe(1);
        var request = handler.Requests[0];
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.ShouldBe("/sessions");
        var body = await request.Content!.ReadAsStringAsync(ct).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.EnumerateObject().Any().ShouldBeFalse();
    }

    /// <summary>List returns every session.</summary>
    [Fact]
    public async Task ListSessionsReturnsAll()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var client = new ApiSessionClient(http);
        using var queued = JsonResponse(HttpStatusCode.OK, new[] { SampleSession(), SampleSession("s-2") });
        handler.Enqueue(queued);

        var sessions = await client.ListSessionsAsync(ct).ConfigureAwait(true);

        sessions.Select(static s => s.Id).ShouldBe(["s-1", "s-2"]);
    }

    /// <summary>Get returns the session, or null on 404.</summary>
    [Fact]
    public async Task GetSessionReturnsNullOnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var client = new ApiSessionClient(http);
        using var found = JsonResponse(HttpStatusCode.OK, SampleSession());
        using var missing = new HttpResponseMessage(HttpStatusCode.NotFound);
        handler.Enqueue(found);
        handler.Enqueue(missing);

        (await client.GetSessionAsync("s-1", ct).ConfigureAwait(true)).ShouldNotBeNull();
        (await client.GetSessionAsync("missing", ct).ConfigureAwait(true)).ShouldBeNull();
        handler.Requests[1].RequestUri!.AbsolutePath.ShouldBe("/sessions/missing");
    }

    /// <summary>Submit posts the prompt to the prompts endpoint.</summary>
    [Fact]
    public async Task SubmitPromptPostsToPromptsEndpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var client = new ApiSessionClient(http);
        using var queued = new HttpResponseMessage(HttpStatusCode.Accepted);
        handler.Enqueue(queued);

        await client.SubmitPromptAsync("s-1", "hello", ct).ConfigureAwait(true);

        var request = handler.Requests[0];
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.ShouldBe("/sessions/s-1/prompts");
        var body = await request.Content!.ReadAsStringAsync(ct).ConfigureAwait(true);
        body.ShouldContain("hello");
    }

    /// <summary>API errors surface as ApiClientException with status and body.</summary>
    [Fact]
    public async Task ApiErrorsThrowApiClientException()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var client = new ApiSessionClient(http);
        using var queued = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("{\"detail\":\"prompt in flight\"}", Encoding.UTF8, "application/problem+json"),
        };
        handler.Enqueue(queued);

        var ex = await Should.ThrowAsync<ApiClientException>(client.SubmitPromptAsync("s-1", "hello", ct)).ConfigureAwait(true);
        ex.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ex.Body.ShouldBe("{\"detail\":\"prompt in flight\"}");
    }

    private static Session SampleSession(string id = "s-1") => new()
    {
        Id = id,
        OwnerId = "local-dev",
        CreatedAt = Stamp,
        UpdatedAt = Stamp,
        Status = SessionStatus.Idle,
        Messages = [],
        Events = [],
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, Session value)
    {
        var json = JsonSerializer.Serialize(value, OmpOsJsonContext.Default.Session);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, Session[] value)
    {
        var json = JsonSerializer.Serialize(value, OmpOsJsonContext.Default.SessionArray);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>An API success body whose declared length exceeds the cap is refused before it is read.</summary>
    [Fact]
    public async Task OversizedApiSuccessBodyIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var client = new ApiSessionClient(http);
        using var oversized = new ByteArrayContent(Encoding.UTF8.GetBytes("[]"))
        {
            Headers = { ContentLength = BoundedContent.MaxSuccessBytes + 1 },
        };
        using var queued = new HttpResponseMessage(HttpStatusCode.OK) { Content = oversized };
        handler.Enqueue(queued);

        await Should.ThrowAsync<ApiClientException>(() => client.ListSessionsAsync(ct)).ConfigureAwait(true);
    }

    private static HttpClient ClientHttp(ScriptedHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
}
