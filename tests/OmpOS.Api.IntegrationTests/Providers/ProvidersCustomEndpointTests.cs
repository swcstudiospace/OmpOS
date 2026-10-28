using System.Net;
using System.Text.Json;
using OmpOS.Api.Services.Providers;
using OmpOS.Harness.OmpConfig;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Providers;

/// <summary>
/// <c>POST /providers/custom</c>, <c>DELETE /providers/custom/{id}</c> and <c>PUT /models/default</c>: off unless the operator enables configuration
/// writes, then the safety table of the base URL, reserved ids, ownership between callers, and the mapping of omp's refusals.
/// </summary>
public sealed class ProvidersCustomEndpointTests
{
    private const string Body = """{"id":"mine","displayName":"Mine","protocol":"openai","baseUrl":"https://llm.example.com/v1","modelIds":["m1","m2"]}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, string?> ConfigOn() => new(StringComparer.Ordinal) { ["Providers:AllowConfigWrite"] = "true" };

    private static string Request(string url = "https://llm.example.com/v1", string id = "mine", string models = """["m1"]""") =>
        $$"""{"id":"{{id}}","protocol":"openai","baseUrl":"{{url}}","modelIds":{{models}}}""";

    [Fact]
    public async Task ConfigurationWritesAreDisabledByDefaultWith403()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend);
        using var client = factory.CreateClient();

        using var add = await ProvidersTestHost.SendAsync(client, HttpMethod.Post, "/providers/custom", Body, Token).ConfigureAwait(true);
        using var remove = await ProvidersTestHost.SendAsync(client, HttpMethod.Delete, "/providers/custom/mine", null, Token).ConfigureAwait(true);
        using var model = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/models/default", """{"model":"a/b"}""", Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(add, HttpStatusCode.Forbidden, "config_writes_disabled", Token).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(remove, HttpStatusCode.Forbidden, "config_writes_disabled", Token).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(model, HttpStatusCode.Forbidden, "config_writes_disabled", Token).ConfigureAwait(true);
        backend.Upserts.ShouldBeEmpty();
        backend.Removals.ShouldBeEmpty();
        backend.Defaults.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnEndpointIsWrittenAsABlockWithTheKeyVariableNameAndListedForItsOwner()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, ConfigOn(), environment: new FakeProviderEnvironment("OMPOS_PROVIDER_MINE_KEY"));
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Post, "/providers/custom", Body, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        body.RootElement.GetProperty("id").GetString().ShouldBe("mine");
        body.RootElement.GetProperty("keyEnvName").GetString().ShouldBe("OMPOS_PROVIDER_MINE_KEY");
        body.RootElement.GetProperty("configured").GetString().ShouldBe("configured");
        body.RootElement.GetProperty("baseUrl").GetString().ShouldBe("https://llm.example.com/v1");
        var block = backend.Upserts.Single();
        block.Id.ShouldBe("mine");
        block.Api.ShouldBe(OmpProviderApi.OpenAiCompletions);
        block.ApiKeyEnvName.ShouldBe("OMPOS_PROVIDER_MINE_KEY");
        block.NoAuthentication.ShouldBeFalse();
        block.BaseUrl.AbsoluteUri.ShouldBe("https://llm.example.com/v1");
        block.Models.Select(static model => model.Id).ShouldBe(["m1", "m2"]);

        using var list = await client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);
        using var listBody = await WebAppTestHost.ReadJsonAsync(list, Token).ConfigureAwait(true);
        listBody.RootElement.GetProperty("custom").EnumerateArray().Select(static item => item.GetProperty("id").GetString()).ShouldBe(["mine"]);
    }

    [Fact]
    public async Task AnAnthropicEndpointWithNoKeyIsWrittenAsNoAuthentication()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, ConfigOn());
        using var client = factory.CreateClient();
        const string Json = """{"id":"local-llm","protocol":"anthropic","baseUrl":"http://127.0.0.1:8080/anthropic","modelIds":["m"],"keyRequired":false}""";

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Post, "/providers/custom", Json, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var block = backend.Upserts.Single();
        block.Api.ShouldBe(OmpProviderApi.AnthropicMessages);
        block.NoAuthentication.ShouldBeTrue();
        block.ApiKeyEnvName.ShouldBeNull();
        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        body.RootElement.TryGetProperty("keyEnvName", out _).ShouldBeFalse();
        body.RootElement.GetProperty("configured").GetString().ShouldBe("not-required");
    }

    [Theory]
    [InlineData("http://llm.example.com/v1")]
    [InlineData("http://169.254.169.254/latest")]
    [InlineData("https://169.254.169.254/latest")]
    [InlineData("https://metadata.google.internal/computeMetadata/v1")]
    [InlineData("https://user:pw@llm.example.com/v1")]
    [InlineData("http://2852039166/")]
    [InlineData("http://0251.0376.0251.0376/")]
    [InlineData("https://[::ffff:169.254.169.254]/")]
    [InlineData("https://[fe80::1]/")]
    [InlineData("file:///etc/passwd")]
    public async Task AnUnsafeBaseUrlIs400NothingIsWrittenAndTheUrlIsNotEchoed(string address)
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, ConfigOn());
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Post, "/providers/custom", Request(address), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid_custom_provider", Token).ConfigureAwait(true);
        (await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true)).ShouldNotContain(address, Case.Insensitive);
        backend.Upserts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Bad Id", """["m"]""")]
    [InlineData("", """["m"]""")]
    [InlineData("mine", "[]")]
    [InlineData("mine", """["bad model"]""")]
    [InlineData("mine", """["a","a"]""")]
    public async Task AnInvalidIdOrModelListIs400(string id, string models)
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, ConfigOn());
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Post, "/providers/custom", Request(id: id, models: models), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid_custom_provider", Token).ConfigureAwait(true);
        backend.Upserts.ShouldBeEmpty();
    }

    [Fact]
    public async Task MoreThanFiftyModelsIs400()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, ConfigOn());
        using var client = factory.CreateClient();
        var models = "[" + string.Join(',', Enumerable.Range(0, 51).Select(static index => $"\"m{index}\"")) + "]";

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Post, "/providers/custom", Request(models: models), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid_custom_provider", Token).ConfigureAwait(true);
    }

    [Theory]
    [InlineData("deepseek")]
    [InlineData("moonshot-cn")]
    [InlineData("openai")]
    [InlineData("llama.cpp")]
    [InlineData("deepseek-anthropic")]
    public async Task AnIdOfTheCatalogueOrOfOmpIs409(string id)
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, ConfigOn());
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Post, "/providers/custom", Request(id: id), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "provider_id_taken", Token).ConfigureAwait(true);
        backend.Upserts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(nameof(ProviderWriteStatus.Rejected), HttpStatusCode.UnprocessableEntity, "custom_provider_rejected")]
    [InlineData(nameof(ProviderWriteStatus.Unavailable), HttpStatusCode.ServiceUnavailable, "omp_unavailable")]
    [InlineData(nameof(ProviderWriteStatus.Conflict), HttpStatusCode.Conflict, "provider_id_taken")]
    public async Task OmpsRefusalIsMappedAndTheEndpointIsNotRemembered(string statusName, HttpStatusCode expected, string code)
    {
        var backend = new FakeProviderBackend { BlockResult = new ProviderWriteResult(Enum.Parse<ProviderWriteStatus>(statusName), "omp said no") };
        using var factory = ProvidersTestHost.Dev(backend, ConfigOn());
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Post, "/providers/custom", Body, Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, expected, code, Token).ConfigureAwait(true);
        using var list = await client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);
        using var listBody = await WebAppTestHost.ReadJsonAsync(list, Token).ConfigureAwait(true);
        listBody.RootElement.GetProperty("custom").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task AnEndpointBelongsToItsCallerAndAnotherCallerCannotSeeReplaceOrRemoveIt()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Jwt(backend, ConfigOn());
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        using var bob = WebAppTestHost.AsPrincipal(factory, "bob");
        using var added = await ProvidersTestHost.SendAsync(alice, HttpMethod.Post, "/providers/custom", Body, Token).ConfigureAwait(true);
        added.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var bobList = await bob.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);
        using var bobTakes = await ProvidersTestHost.SendAsync(bob, HttpMethod.Post, "/providers/custom", Body, Token).ConfigureAwait(true);
        using var bobRemoves = await ProvidersTestHost.SendAsync(bob, HttpMethod.Delete, "/providers/custom/mine", null, Token).ConfigureAwait(true);
        using var bobTests = await bob.PostAsync(new Uri("/providers/mine/test", UriKind.Relative), content: null, Token).ConfigureAwait(true);

        using var bobBody = await WebAppTestHost.ReadJsonAsync(bobList, Token).ConfigureAwait(true);
        bobBody.RootElement.GetProperty("custom").GetArrayLength().ShouldBe(0);
        await WebAppTestHost.ShouldBeProblemAsync(bobTakes, HttpStatusCode.Conflict, "provider_id_taken", Token).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(bobRemoves, HttpStatusCode.NotFound, "provider_not_found", Token).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(bobTests, HttpStatusCode.NotFound, "provider_not_found", Token).ConfigureAwait(true);
        backend.Upserts.Count.ShouldBe(1, "bob's attempt never reached omp");
        backend.Removals.ShouldBeEmpty();

        using var aliceReplaces = await ProvidersTestHost.SendAsync(alice, HttpMethod.Post, "/providers/custom", Body.Replace("m1", "m3", StringComparison.Ordinal), Token).ConfigureAwait(true);
        using var aliceRemoves = await ProvidersTestHost.SendAsync(alice, HttpMethod.Delete, "/providers/custom/mine", null, Token).ConfigureAwait(true);
        aliceReplaces.StatusCode.ShouldBe(HttpStatusCode.OK);
        aliceRemoves.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        backend.Removals.ShouldBe(["mine"]);
        using var aliceList = await alice.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);
        using var aliceBody = await WebAppTestHost.ReadJsonAsync(aliceList, Token).ConfigureAwait(true);
        aliceBody.RootElement.GetProperty("custom").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ARemovalOmpRefusesKeepsTheEndpointOfItsOwner()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, ConfigOn());
        using var client = factory.CreateClient();
        using var added = await ProvidersTestHost.SendAsync(client, HttpMethod.Post, "/providers/custom", Body, Token).ConfigureAwait(true);
        backend.BlockResult = new ProviderWriteResult(ProviderWriteStatus.Unavailable, "omp could not be run");

        using var removed = await ProvidersTestHost.SendAsync(client, HttpMethod.Delete, "/providers/custom/mine", null, Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(removed, HttpStatusCode.ServiceUnavailable, "omp_unavailable", Token).ConfigureAwait(true);
        using var list = await client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);
        using var listBody = await WebAppTestHost.ReadJsonAsync(list, Token).ConfigureAwait(true);
        listBody.RootElement.GetProperty("custom").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task TheDefaultModelIsSetThroughOmpAndAnInvalidSelectorIs400()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, ConfigOn());
        using var client = factory.CreateClient();

        using var ok = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/models/default", """{"model":"deepseek/deepseek-chat"}""", Token).ConfigureAwait(true);
        using var bad = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/models/default", """{"model":"no-slash"}""", Token).ConfigureAwait(true);
        using var missing = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/models/default", "{}", Token).ConfigureAwait(true);

        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(ok, Token).ConfigureAwait(true);
        body.RootElement.GetProperty("model").GetString().ShouldBe("deepseek/deepseek-chat");
        await WebAppTestHost.ShouldBeProblemAsync(bad, HttpStatusCode.BadRequest, "invalid_model", Token).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(missing, HttpStatusCode.BadRequest, "invalid_model", Token).ConfigureAwait(true);
        backend.Defaults.ShouldBe(["deepseek/deepseek-chat"]);
    }

    [Fact]
    public async Task ADefaultModelOmpRefusesIs422()
    {
        var backend = new FakeProviderBackend { DefaultResult = new ProviderWriteResult(ProviderWriteStatus.Rejected, "omp has no such model") };
        using var factory = ProvidersTestHost.Dev(backend, ConfigOn());
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/models/default", """{"model":"a/b"}""", Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "model_rejected", Token).ConfigureAwait(true);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        document.RootElement.GetProperty("detail").GetString().ShouldBe("omp has no such model");
    }
}
