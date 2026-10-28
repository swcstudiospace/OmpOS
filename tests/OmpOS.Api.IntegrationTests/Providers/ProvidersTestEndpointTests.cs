using System.Net;
using System.Text.Json;
using OmpOS.Api.Services.Providers;
using OmpOS.Contracts.Providers;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Providers;

/// <summary><c>POST /providers/{id}/test</c>: skipped with a reason unless the operator allows live calls (OQ-71), then one keyless call to the provider address.</summary>
public sealed class ProvidersTestEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> TestAsync(HttpClient client, string id) =>
        client.PostAsync(new Uri($"/providers/{id}/test", UriKind.Relative), content: null, Token);

    [Fact]
    public async Task ByDefaultTheTestIsSkippedWithAReasonAndNothingIsCalled()
    {
        var probe = new FakeProviderProbe();
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend(), probe: probe);
        using var client = factory.CreateClient();

        using var response = await TestAsync(client, "moonshot-cn").ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        body.RootElement.GetProperty("status").GetString().ShouldBe("skipped");
        body.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("providerId").GetString().ShouldBe("moonshot-cn");
        probe.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnUnknownProviderIs404EvenWhenTestsAreDisabled()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();

        using var response = await TestAsync(client, "no-such-provider").ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "provider_not_found", Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task WhenAllowedTheDefaultRouteAddressIsProbedOnce()
    {
        var probe = new FakeProviderProbe { Result = new ProviderProbeResult(ProviderTestStatus.Unauthorized, "asks for a key") };
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend(), new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowLiveTest"] = "true" }, probe);
        using var client = factory.CreateClient();

        using var response = await TestAsync(client, "moonshot-cn").ConfigureAwait(true);

        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        body.RootElement.GetProperty("status").GetString().ShouldBe("unauthorized");
        body.RootElement.GetProperty("reason").GetString().ShouldBe("asks for a key");
        probe.Calls.Single().AbsoluteUri.ShouldBe("https://api.moonshot.cn/v1");
    }

    [Theory]
    [InlineData("openai-codex")]
    [InlineData("dashscope-intl")]
    public async Task ASignInRowAndARowWithPlaceholdersAreSkippedEvenWhenAllowed(string id)
    {
        var probe = new FakeProviderProbe();
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend(), new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowLiveTest"] = "true" }, probe);
        using var client = factory.CreateClient();

        using var response = await TestAsync(client, id).ConfigureAwait(true);

        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        body.RootElement.GetProperty("status").GetString().ShouldBe("skipped");
        body.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        probe.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACustomEndpointIsProbedAtItsStoredAddressForItsOwner()
    {
        var probe = new FakeProviderProbe();
        var settings = ProvidersTestHost.AllOn();
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend(), settings, probe);
        using var client = factory.CreateClient();
        using var added = await ProvidersTestHost.SendAsync(client, HttpMethod.Post, "/providers/custom", """{"id":"mine","protocol":"openai","baseUrl":"https://llm.example.com/v1/","modelIds":["m"]}""", Token).ConfigureAwait(true);
        added.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await TestAsync(client, "mine").ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        probe.Calls.Single().AbsoluteUri.ShouldBe("https://llm.example.com/v1");
    }

    [Fact]
    public async Task ABlockedAddressIsReportedAsBlockedNotAsAnError()
    {
        var probe = new FakeProviderProbe { Result = new ProviderProbeResult(ProviderTestStatus.Blocked, "not on the public internet") };
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend(), new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowLiveTest"] = "true" }, probe);
        using var client = factory.CreateClient();

        using var response = await TestAsync(client, "ollama").ConfigureAwait(true);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        document.RootElement.GetProperty("status").GetString().ShouldBe("blocked");
    }
}
