using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>Guards the new-conversation button against the label-content-name-mismatch finding.</summary>
public sealed class NewConversationButtonTests : BunitContext
{
    /// <summary>The button's accessible name contains its visible label.</summary>
    [Fact]
    public void NewConversationButtonNameContainsVisibleLabel()
    {
        using var apiHandler = new ScriptedHandler();
        using var sseHandler = new ScriptedHandler();
        using var listResponse = JsonResponse("[]");
        using var createdResponse = JsonResponse(SessionJson);
        using var fetchedResponse = JsonResponse(SessionJson);
        apiHandler.Enqueue(listResponse);
        apiHandler.Enqueue(createdResponse);
        apiHandler.Enqueue(fetchedResponse);
        using var apiHttp = new HttpClient(apiHandler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
        using var sseHttp = new HttpClient(sseHandler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
        Services.AddSingleton(new ApiSessionClient(apiHttp));
        Services.AddSingleton(new SseEventStream(sseHttp));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<OmpOS.Web.Pages.Index>();
        cut.WaitForAssertion(() => cut.Find("button.ompos-btn").ShouldNotBeNull());

        var button = cut.FindAll("button.ompos-btn").Single(b => b.TextContent.Contains("New", StringComparison.Ordinal));
        var visibleLabel = button.TextContent.Trim();
        visibleLabel.ShouldContain("New");
        var accessibleName = button.GetAttribute("aria-label");
        accessibleName.ShouldNotBeNullOrWhiteSpace();
        accessibleName.ShouldContain("New", customMessage: "the accessible name must contain the visible label");
    }

    /// <summary>Builds a JSON success response for a scripted API call.</summary>
    /// <param name="json">The response body.</param>
    /// <returns>The queued response message.</returns>
    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    /// <summary>Gets a minimal session body for the create/get round-trips.</summary>
    private const string SessionJson = """{"id":"new-button-test","ownerId":"local-dev","createdAt":"2026-10-21T00:00:00+00:00","updatedAt":"2026-10-21T00:00:00+00:00","status":"Idle","sandboxId":null,"messages":[],"events":[]}""";
}
