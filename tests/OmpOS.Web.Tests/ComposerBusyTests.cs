using System.Net;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>The composer returns to idle on every terminal path: a completed turn and a refused send.</summary>
public sealed class ComposerBusyTests : BunitContext
{
    /// <summary>A completed turn flips the composer back from Stop to Send with no error shown.</summary>
    [Fact]
    public async Task CompletedTurnReleasesTheComposer()
    {
        using var apiHandler = new ScriptedHandler();
        using var sseHandler = new ScriptedHandler();
        using var listed = Json("[]");
        using var created = Json(SessionJson);
        using var fetched = Json(SessionJson);
        using var accepted = Accepted();
        apiHandler.Enqueue(listed);
        apiHandler.Enqueue(created);
        apiHandler.Enqueue(fetched);
        apiHandler.Enqueue(accepted);
        var gate = new GatedSseContent(
            SseFixtures.Frame(SseFixtures.Text(1, "hi"), "text_delta"),
            SseFixtures.Frame(SseFixtures.Completed(2), "completed"));
        using var streamed = Sse(gate);
        sseHandler.Enqueue(streamed);
        using var apiHttp = new HttpClient(apiHandler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
        using var sseHttp = new HttpClient(sseHandler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
        Services.AddSingleton(new ApiSessionClient(apiHttp));
        Services.AddSingleton(new SseEventStream(sseHttp));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<OmpOS.Web.Pages.Index>();
        await cut.WaitForAssertionAsync(() =>
        {
            cut.Find("textarea").ShouldNotBeNull();
            return Task.CompletedTask;
        }).ConfigureAwait(true);

        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "hello" }).ConfigureAwait(true);
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }).ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() =>
        {
            cut.Find("button[aria-label='Stop response']").ShouldNotBeNull();
            return Task.CompletedTask;
        }).ConfigureAwait(true);

        gate.Release();
        await cut.WaitForAssertionAsync(() =>
        {
            cut.Find("button[aria-label='Send message']").ShouldNotBeNull();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
        cut.FindAll("[role='alert']").ShouldBeEmpty();
    }

    /// <summary>A refused send shows the error and leaves the composer idle instead of stuck.</summary>
    [Fact]
    public async Task RefusedSendShowsErrorAndKeepsComposerIdle()
    {
        using var apiHandler = new ScriptedHandler();
        using var sseHandler = new ScriptedHandler();
        using var listed = Json("[]");
        using var created = Json(SessionJson);
        using var fetched = Json(SessionJson);
        using var refused = Refused();
        apiHandler.Enqueue(listed);
        apiHandler.Enqueue(created);
        apiHandler.Enqueue(fetched);
        apiHandler.Enqueue(refused);
        using var apiHttp = new HttpClient(apiHandler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
        using var sseHttp = new HttpClient(sseHandler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
        Services.AddSingleton(new ApiSessionClient(apiHttp));
        Services.AddSingleton(new SseEventStream(sseHttp));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<OmpOS.Web.Pages.Index>();
        await cut.WaitForAssertionAsync(() =>
        {
            cut.Find("textarea").ShouldNotBeNull();
            return Task.CompletedTask;
        }).ConfigureAwait(true);

        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "hello" }).ConfigureAwait(true);
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() =>
        {
            cut.Find("p.ompos-error").TextContent.ShouldContain("Could not send");
            cut.Find("button[aria-label='Send message']").ShouldNotBeNull();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Builds a JSON success response for a scripted API call.</summary>
    /// <param name="json">The response body.</param>
    /// <returns>The queued response message.</returns>
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    /// <summary>Builds the accepted response for a scripted prompt submit.</summary>
    /// <returns>The queued response message.</returns>
    private static HttpResponseMessage Accepted() => new(HttpStatusCode.Accepted);

    /// <summary>Builds the refused response for a scripted prompt submit.</summary>
    /// <returns>The queued response message.</returns>
    private static HttpResponseMessage Refused() => new(HttpStatusCode.InternalServerError)
    {
        Content = new StringContent("busy", Encoding.UTF8, "application/json"),
    };

    /// <summary>Builds the SSE response serving the given content.</summary>
    /// <param name="content">The event-stream content.</param>
    /// <returns>The queued response message.</returns>
    private static HttpResponseMessage Sse(HttpContent content) => new(HttpStatusCode.OK)
    {
        Content = content,
    };

    /// <summary>Gets a minimal session body for the create/get round-trips.</summary>
    private const string SessionJson = """{"id":"composer-busy-test","ownerId":"local-dev","createdAt":"2026-10-21T00:00:00+00:00","updatedAt":"2026-10-21T00:00:00+00:00","status":"Idle","sandboxId":null,"messages":[],"events":[]}""";
}
