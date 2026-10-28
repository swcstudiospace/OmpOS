using System.Net;
using Autonogrammer.Api.Services.Providers;
using Autonogrammer.Harness.Providers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Providers;

/// <summary>
/// <c>PUT /providers/{id}/credential</c>: disabled with 403 unless the operator enables it (OQ-104), then write-only. The key reaches omp once,
/// and no response, problem or log line repeats it.
/// </summary>
public sealed class ProvidersCredentialEndpointTests
{
    private const string Canary = "sk-canary-api-key-9a7c1e5d3b2f4086";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string KeyBody(string key = Canary, bool? plan = null) =>
        plan is null ? $$"""{"apiKey":"{{key}}"}""" : $$"""{"apiKey":"{{key}}","planRuleAccepted":{{(plan.Value ? "true" : "false")}}}""";

    [Fact]
    public async Task WritesAreDisabledByDefaultWith403AndTheKeyIsNeverRead()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend);
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/deepseek/credential", KeyBody(), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "credential_writes_disabled", Token).ConfigureAwait(true);
        (await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true)).ShouldNotContain(Canary, Case.Sensitive);
        backend.Imports.ShouldBeEmpty();
    }

    [Fact]
    public async Task WritesAreDisabledEvenForAnUnknownIdAndAMalformedBody()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();

        using var unknown = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/no-such/credential", KeyBody(), Token).ConfigureAwait(true);
        using var malformed = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/deepseek/credential", "{not json", Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(unknown, HttpStatusCode.Forbidden, "credential_writes_disabled", Token).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(malformed, HttpStatusCode.Forbidden, "credential_writes_disabled", Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task AnEnabledWriteHandsTheKeyToOmpOnceAndTheAnswerHasNoKey()
    {
        var backend = new FakeProviderBackend();
        var logs = new CapturingLoggerProvider();
        using var factory = ProvidersTestHost.Dev(backend, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowCredentialWrite"] = "true" }, logger: logs);
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/deepseek/credential", KeyBody(), Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        text.ShouldBe("""{"providerId":"deepseek","configured":"configured"}""");
        backend.Imports.Single().ShouldBe(("deepseek", Canary));
        logs.Messages.ShouldNotContain(message => message.Contains(Canary, StringComparison.Ordinal));
        logs.Entries.ShouldNotContain(entry => entry.Message.Contains(Canary, StringComparison.Ordinal));
        response.Headers.SelectMany(static header => header.Value).ShouldNotContain(value => value.Contains(Canary, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AWriteMakesTheNextListReadOmpAgain()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowCredentialWrite"] = "true", ["Providers:StateCacheSeconds"] = "3600" });
        using var client = factory.CreateClient();
        using var first = await client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);
        using var second = await client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);
        backend.Reads.ShouldBe(1, "the state is reused inside the cache window");

        using var write = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/deepseek/credential", KeyBody(), Token).ConfigureAwait(true);
        using var third = await client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);

        write.StatusCode.ShouldBe(HttpStatusCode.OK);
        backend.Reads.ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    [InlineData("tab\\there")]
    public async Task AKeyThatIsBlankOrHasWhitespaceIs400AndNotEchoed(string key)
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowCredentialWrite"] = "true" });
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/deepseek/credential", KeyBody(key), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid_credential", Token).ConfigureAwait(true);
        (await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true)).ShouldNotContain("two words", Case.Sensitive);
        backend.Imports.ShouldBeEmpty();
    }

    [Fact]
    public async Task AKeyOverTheLimitIs400()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowCredentialWrite"] = "true" });
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/deepseek/credential", KeyBody(new string('k', 513)), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid_credential", Token).ConfigureAwait(true);
        backend.Imports.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnUnknownProviderIs404AndABodyThatIsNotJsonIs400()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend(), new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowCredentialWrite"] = "true" });
        using var client = factory.CreateClient();

        using var unknown = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/no-such/credential", KeyBody(), Token).ConfigureAwait(true);
        using var malformed = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/deepseek/credential", "{not json", Token).ConfigureAwait(true);
        using var wrongType = new HttpRequestMessage(HttpMethod.Put, new Uri("/providers/deepseek/credential", UriKind.Relative)) { Content = new StringContent(KeyBody(), System.Text.Encoding.UTF8, "text/plain") };
        using var unsupported = await client.SendAsync(wrongType, Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(unknown, HttpStatusCode.NotFound, "provider_not_found", Token).ConfigureAwait(true);
        malformed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        unsupported.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task ASignInRowAndAKeylessRowTakeNoKey()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowCredentialWrite"] = "true" });
        using var client = factory.CreateClient();

        using var signIn = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/openai-codex/credential", KeyBody(plan: true), Token).ConfigureAwait(true);
        using var keyless = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/ollama/credential", KeyBody(), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(signIn, HttpStatusCode.Conflict, "credential_not_applicable", Token).ConfigureAwait(true);
        (await signIn.Content.ReadAsStringAsync(Token).ConfigureAwait(true)).ShouldContain("/login openai-codex", Case.Sensitive);
        await WebAppTestHost.ShouldBeProblemAsync(keyless, HttpStatusCode.Conflict, "credential_not_applicable", Token).ConfigureAwait(true);
        backend.Imports.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARowOmpDoesNotKnowIsOperatorManagedAndNamesTheVariable()
    {
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowCredentialWrite"] = "true" });
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/zai-api/credential", KeyBody(), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "credential_operator_managed", Token).ConfigureAwait(true);
        var text = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        text.ShouldContain("AUTONOGRAMMER_PROVIDER_ZAI_API_KEY", Case.Sensitive);
        text.ShouldNotContain(Canary, Case.Sensitive);
        backend.Imports.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACodingPlanNeedsTheRuleTextToBeConfirmed()
    {
        var plan = CatalogueRows.All.First(static row => row.Plan is not null && row.IsOmpNative && row.Routes.Count > 0 && row.UrlVariables.Count == 0);
        var backend = new FakeProviderBackend();
        using var factory = ProvidersTestHost.Dev(backend, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowCredentialWrite"] = "true" });
        using var client = factory.CreateClient();

        using var refused = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, $"/providers/{plan.Id}/credential", KeyBody(), Token).ConfigureAwait(true);
        backend.Imports.ShouldBeEmpty();
        using var accepted = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, $"/providers/{plan.Id}/credential", KeyBody(plan: true), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(refused, HttpStatusCode.BadRequest, "plan_rule_not_accepted", Token).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        backend.Imports.Single().ShouldBe((plan.OmpProviderId!, Canary));
    }

    [Theory]
    [InlineData(nameof(ProviderWriteStatus.Rejected), HttpStatusCode.UnprocessableEntity, "credential_rejected")]
    [InlineData(nameof(ProviderWriteStatus.Unavailable), HttpStatusCode.ServiceUnavailable, "omp_unavailable")]
    public async Task AFailureOfOmpBecomesAProblemThatCannotRepeatTheKey(string statusName, HttpStatusCode expected, string code)
    {
        var backend = new FakeProviderBackend { ImportResult = new ProviderWriteResult(Enum.Parse<ProviderWriteStatus>(statusName), "omp said no to " + Canary) };
        using var factory = ProvidersTestHost.Dev(backend, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:AllowCredentialWrite"] = "true" });
        using var client = factory.CreateClient();

        using var response = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/deepseek/credential", KeyBody(), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, expected, code, Token).ConfigureAwait(true);
        var text = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        text.ShouldNotContain(Canary, Case.Sensitive);
        text.ShouldContain("<key>", Case.Sensitive);
    }

    [Fact]
    public async Task NoOtherResponseOfAWriteSessionHoldsTheKey()
    {
        var backend = new FakeProviderBackend();
        var logs = new CapturingLoggerProvider();
        using var factory = ProvidersTestHost.Dev(backend, ProvidersTestHost.AllOn(), logger: logs);
        using var client = factory.CreateClient();
        using var write = await ProvidersTestHost.SendAsync(client, HttpMethod.Put, "/providers/deepseek/credential", KeyBody(), Token).ConfigureAwait(true);

        using var list = await client.GetAsync(new Uri("/providers", UriKind.Relative), Token).ConfigureAwait(true);
        using var one = await client.GetAsync(new Uri("/providers/deepseek", UriKind.Relative), Token).ConfigureAwait(true);
        using var test = await client.PostAsync(new Uri("/providers/deepseek/test", UriKind.Relative), content: null, Token).ConfigureAwait(true);

        foreach (var response in new[] { write, list, one, test })
        {
            (await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true)).ShouldNotContain(Canary, Case.Sensitive);
        }

        logs.Messages.ShouldNotContain(message => message.Contains(Canary, StringComparison.Ordinal));
    }
}
