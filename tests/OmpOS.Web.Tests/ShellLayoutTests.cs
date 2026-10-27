using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>The web head's page is the app shell: landmarks, the agent rail around the existing conversation list, the thread in main, and the source offer in the footer (PX-08).</summary>
public sealed class ShellLayoutTests : BunitContext
{
    /// <summary>The page exposes banner, an Agents navigation, main and contentinfo; the New button and the conversations sit in the navigation, the transcript and composer in main.</summary>
    [Fact]
    public void PageIsTheShellWithRailThreadAndFooter()
    {
        var cut = RenderPageWithOneSession();

        cut.FindAll("header").Count.ShouldBe(1);
        var nav = cut.Find("nav[aria-label='Agents']");
        nav.QuerySelector("button[aria-label='New conversation']").ShouldNotBeNull();
        nav.QuerySelectorAll("button.ompos-conversation-item").Length.ShouldBe(1);
        var main = cut.Find("main#main");
        main.QuerySelector("section#ompos-conversation").ShouldNotBeNull();
        main.QuerySelector("textarea[aria-label='Message']").ShouldNotBeNull();
        main.QuerySelector("button[aria-label='New conversation']").ShouldBeNull();
        cut.FindAll("nav").Count.ShouldBe(1, "the conversation list lives inside the Agents navigation, not in a second navigation landmark");
        cut.Find(".ompos-shell").ShouldNotBeNull();
    }

    /// <summary>Every screen carries the AGPL source offer: the repository link, the licence and the version.</summary>
    [Fact]
    public void FooterCarriesTheSourceOffer()
    {
        var cut = RenderPageWithOneSession();

        var footer = cut.Find("footer");
        footer.QuerySelector("a[href='https://github.com/swcstudiospace/OmpOS']").ShouldNotBeNull();
        footer.TextContent.ShouldContain("AGPL-3.0-or-later");
        footer.QuerySelector(".ompos-footer-version")!.TextContent.ShouldStartWith("OmpOS ");
    }

    /// <summary>The theme switch sits in the top bar, and the top bar names the workspace and links the source.</summary>
    [Fact]
    public void TopBarHoldsTheThemeSwitchWorkspaceAndSourceLink()
    {
        var cut = RenderPageWithOneSession();

        var header = cut.Find("header");
        header.QuerySelectorAll(".ompos-theme-switch button").Length.ShouldBe(3);
        header.QuerySelector(".ompos-workspace-label").ShouldNotBeNull();
        header.QuerySelector("a.ompos-topbar-link[href='https://github.com/swcstudiospace/OmpOS']").ShouldNotBeNull();
    }

    /// <summary>Renders the page against a scripted API that creates one session, and waits until its conversation is listed.</summary>
    /// <returns>The rendered page.</returns>
    private IRenderedComponent<OmpOS.Web.Pages.Index> RenderPageWithOneSession()
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
    private const string SessionJson = """{"id":"shell-layout-test","ownerId":"local-dev","createdAt":"2026-10-27T00:00:00+00:00","updatedAt":"2026-10-27T00:00:00+00:00","status":"Idle","sandboxId":null,"messages":[],"events":[]}""";
}
