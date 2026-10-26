using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.UI.Components;
using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>The icon-only send control is named by its label alone, and the skip link is the first element a Tab reaches in the session shell.</summary>
public sealed class SendControlAndSkipLinkTests : BunitContext
{
    /// <summary>Matches the elements a single Tab can reach, in document order.</summary>
    private const string FocusableSelector = "a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])";

    /// <summary>The send control holds no text, so its accessible name is exactly its aria-label and its arrow icon is hidden from assistive technology.</summary>
    [Fact]
    public void SendControlHasNoTextAndIsNamedByItsLabel()
    {
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, (string _) => Task.FromResult(true)));

        var send = cut.Find("button[aria-label='Send message']");
        send.TextContent.Trim().ShouldBe(string.Empty, customMessage: "an icon-only control must not expose text that could join its accessible name");
        send.GetAttribute("aria-label").ShouldBe("Send message");
        var icon = send.QuerySelector("svg");
        icon.ShouldNotBeNull();
        icon!.GetAttribute("aria-hidden").ShouldBe("true");
    }

    /// <summary>In the rendered session shell, the skip link is the first element a single Tab reaches, ahead of the sidebar and the New button.</summary>
    [Fact]
    public void SkipLinkIsTheFirstFocusableElementInTheShell()
    {
        var cut = RenderShellWithSession();

        var focusable = cut.FindAll(FocusableSelector);
        focusable.Count.ShouldBeGreaterThan(1);
        var first = focusable[0];
        first.TagName.ShouldBe("A");
        first.ClassList.Contains("ompos-skip-link").ShouldBeTrue();
        cut.FindAll("a.ompos-skip-link").Count.ShouldBe(1);
    }

    /// <summary>The skip link targets the conversation region, which exists in the shell and can take programmatic focus.</summary>
    [Fact]
    public void SkipLinkTargetExistsAndAcceptsProgrammaticFocus()
    {
        var cut = RenderShellWithSession();

        var href = cut.Find("a.ompos-skip-link").GetAttribute("href") ?? string.Empty;
        href.ShouldStartWith("#");
        var target = cut.Find(href);
        target.GetAttribute("tabindex").ShouldBe("-1");
    }

    /// <summary>Renders the Web shell against a scripted API that creates one session, and waits until its conversation is listed.</summary>
    /// <returns>The rendered shell.</returns>
    private IRenderedComponent<OmpOS.Web.Pages.Index> RenderShellWithSession()
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
        cut.WaitForAssertion(() => cut.Find("button.ompos-conversation-item").ShouldNotBeNull());
        return cut;
    }

    /// <summary>Builds a JSON success response for a scripted API call.</summary>
    /// <param name="json">The response body.</param>
    /// <returns>The queued response message.</returns>
    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    /// <summary>Gets a minimal session body for the create and get round-trips.</summary>
    private const string SessionJson = """{"id":"send-control-skip-link-test","ownerId":"local-dev","createdAt":"2026-10-21T00:00:00+00:00","updatedAt":"2026-10-21T00:00:00+00:00","status":"Idle","sandboxId":null,"messages":[],"events":[]}""";
}
