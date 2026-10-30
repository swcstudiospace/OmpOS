using Autonogrammer.Harness.Providers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

/// <summary>
/// Turning catalogue routes into managed providers: every route of every row produces a valid block that omp's schema accepts and the
/// reader reads back, the key is only ever an environment variable name, URL variables are resolved before anything is written, and
/// http, placeholders and unresolved variables are refused.
/// </summary>
public sealed class OmpCatalogueBlockTests
{
    private const string UserModel = "my-model-1";

    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>(StringComparer.Ordinal);

    public static TheoryData<string, string> EveryRoute()
    {
        var data = new TheoryData<string, string>();
        foreach (var row in CatalogueRows.All)
        {
            foreach (var route in row.Routes)
            {
                data.Add(row.Id, route.Id);
            }
        }

        return data;
    }

    private static Dictionary<string, string> SampleValues(CatalogueRow row) =>
        row.UrlVariables.ToDictionary(variable => variable.Name, variable => "acct-1." + variable.Name.Replace('_', '-'), StringComparer.Ordinal);

    private static OmpCatalogueBlockResult Build(string rowId, string routeId, string? envName = null, IReadOnlyList<string>? models = null)
    {
        var row = CatalogueRows.Find(rowId).ShouldNotBeNull(rowId);
        var route = row.Routes.Single(candidate => string.Equals(candidate.Id, routeId, StringComparison.Ordinal));
        return OmpCatalogueBlock.Build(row, route, SampleValues(row), envName, models);
    }

    [Theory]
    [MemberData(nameof(EveryRoute))]
    public void EveryRouteOfEveryRowYieldsAValidBlockOmpCanRead(string rowId, string routeId)
    {
        var row = CatalogueRows.Find(rowId)!;
        var result = Build(rowId, routeId, models: [UserModel]);

        var provider = result.Provider.ShouldNotBeNull($"{rowId}/{routeId}: {result.Problem}");
        OmpManagedProviderValidator.Validate(provider).ShouldBeNull($"{rowId}/{routeId}");
        provider.Id.ShouldBe(row.BlockProviderId(row.Routes.Single(route => string.Equals(route.Id, routeId, StringComparison.Ordinal))));
        provider.BaseUrl.OriginalString.ShouldNotContain("{", Case.Sensitive);
        if (row.Region != CatalogueRegion.Local)
        {
            provider.BaseUrl.Scheme.ShouldBe(Uri.UriSchemeHttps, rowId);
        }

        var lines = OmpManagedBlockWriter.Render(provider, 2, 2);
        var inner = lines.Skip(1).Take(lines.Count - 2).ToList();
        OmpManagedBlockReader.TryParse(provider.Id, inner, out var parsed, out var problem).ShouldBeTrue($"{rowId}/{routeId}: {problem}");
        parsed.ShouldNotBeNull().Api.ShouldBe(provider.Api);
    }

    [Theory]
    [MemberData(nameof(EveryRoute))]
    public void TheBlockNamesTheKeyAsAnEnvironmentVariableOrWritesAuthNone(string rowId, string routeId)
    {
        var row = CatalogueRows.Find(rowId)!;
        var route = row.Routes.Single(candidate => string.Equals(candidate.Id, routeId, StringComparison.Ordinal));
        var provider = Build(rowId, routeId, models: [UserModel]).Provider.ShouldNotBeNull();

        if (route.Auth == CatalogueAuthStyle.None)
        {
            provider.NoAuthentication.ShouldBeTrue();
            provider.ApiKeyEnvName.ShouldBeNull();
        }
        else
        {
            provider.NoAuthentication.ShouldBeFalse();
            provider.ApiKeyEnvName.ShouldBe(row.ApiKeyEnv, rowId);
            OmpManagedProviderValidator.IsEnvName(provider.ApiKeyEnvName).ShouldBeTrue(rowId);
        }
    }

    [Fact]
    public void AnOpenAiRouteWithADocumentedModelsListUsesDiscoveryAndNeedsNoModelId()
    {
        var mistral = Build("mistral", "openai").Provider.ShouldNotBeNull();

        mistral.Discovery.ShouldBe(new OmpManagedDiscovery(OmpManagedDiscovery.OpenAiModelsList));
        mistral.Models.ShouldBeEmpty();
    }

    [Fact]
    public void ARouteWithNoModelsListAsksForAModelId()
    {
        var result = Build("perplexity", "responses");

        result.Succeeded.ShouldBeFalse();
        result.Problem.ShouldNotBeNull().ShouldContain("model id", Case.Sensitive);
        Build("perplexity", "responses", models: ["sonar"]).Provider.ShouldNotBeNull().Models.Select(model => model.Id).ShouldBe(["sonar"]);
    }

    [Fact]
    public void AnAnthropicRouteNeverDiscoversModelsAndSendsTheBearerHeaderForBearerAuth()
    {
        var provider = Build("deepseek", "anthropic", models: ["deepseek-chat"]).Provider.ShouldNotBeNull();

        provider.Api.ShouldBe(OmpProviderApi.AnthropicMessages);
        provider.Discovery.ShouldBeNull();
        provider.BearerAuthHeader.ShouldBeTrue();
        provider.Id.ShouldBe("deepseek-anthropic");

        var xApiKey = Build("anthropic", "anthropic-messages", models: ["claude-x"]).Provider.ShouldNotBeNull();
        xApiKey.BearerAuthHeader.ShouldBeFalse();
    }

    [Fact]
    public void ACustomHeaderRouteWritesTheHeaderWithTheEnvNameAndNoKey()
    {
        var provider = Build("gcore-everywhere-inference", "openai", models: [UserModel]).Provider.ShouldNotBeNull();

        provider.KeyHeaderName.ShouldBe("X-API-Key");
        var text = string.Join('\n', OmpManagedBlockWriter.Render(provider, 2, 2));
        text.ShouldContain("\"X-API-Key\": \"" + provider.ApiKeyEnvName + "\"", Case.Sensitive);
    }

    [Fact]
    public void TheCallersEnvNameWinsOverTheRowsOwnName()
    {
        var provider = Build("scaleway", "openai", envName: "AUTONOGRAMMER_KEY_SCALEWAY", models: ["glm-5.3"]).Provider.ShouldNotBeNull();

        provider.ApiKeyEnvName.ShouldBe("AUTONOGRAMMER_KEY_SCALEWAY");
    }

    [Fact]
    public void ALiteralKeyInPlaceOfAnEnvNameIsRefused()
    {
        var result = Build("scaleway", "openai", envName: "sk-live-abc123def456", models: ["glm-5.3"]);

        result.Succeeded.ShouldBeFalse();
        result.Problem.ShouldNotBeNull().ShouldContain("environment variable", Case.Sensitive);
        result.Problem.ShouldNotContain("abc123", Case.Sensitive);
    }

    [Fact]
    public void UrlVariablesAreResolvedIntoTheBaseUrl()
    {
        var row = CatalogueRows.Find("infomaniak-ai-tools")!;
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["product_id"] = "424242" };

        var provider = OmpCatalogueBlock.Build(row, row.Routes[0], values, null, [UserModel]).Provider.ShouldNotBeNull();

        provider.BaseUrl.AbsoluteUri.ShouldBe("https://api.infomaniak.com/2/ai/424242/openai/v1");
    }

    [Fact]
    public void AnUnresolvedOrPlaceholderVariableIsRefused()
    {
        var row = CatalogueRows.Find("infomaniak-ai-tools")!;

        OmpCatalogueBlock.Build(row, row.Routes[0], NoValues, null, [UserModel]).Problem.ShouldBe("Enter a value for product_id.");
        var placeholder = new Dictionary<string, string>(StringComparer.Ordinal) { ["product_id"] = "your-product-id" };
        OmpCatalogueBlock.Build(row, row.Routes[0], placeholder, null, [UserModel]).Succeeded.ShouldBeFalse();
        var injection = new Dictionary<string, string>(StringComparer.Ordinal) { ["product_id"] = "1/../../x" };
        OmpCatalogueBlock.Build(row, row.Routes[0], injection, null, [UserModel]).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void PlainHttpToARemoteHostIsRefusedButLoopbackIsAcceptedForALocalRuntime()
    {
        var remote = CatalogueRows.Find("scaleway")!;
        var httpRoute = remote.Routes[0] with { BaseAddressTemplate = "http://api.scaleway.ai/v1" };

        OmpCatalogueBlock.Build(remote, httpRoute, NoValues, null, ["glm-5.3"]).Problem.ShouldNotBeNull().ShouldContain("https", Case.Sensitive);

        var local = Build("ollama", "openai").Provider.ShouldNotBeNull();
        local.BaseUrl.Scheme.ShouldBe(Uri.UriSchemeHttp);
        local.NoAuthentication.ShouldBeTrue();
        OmpCatalogueBlock.Build(remote with { Region = CatalogueRegion.Local }, httpRoute, NoValues, null, ["glm-5.3"]).Problem.ShouldNotBeNull();
    }

    [Fact]
    public void AnApiValueOmpDoesNotKnowIsRefused()
    {
        var row = CatalogueRows.Find("scaleway")!;

        OmpCatalogueBlock.Build(row, row.Routes[0] with { Api = "openai-chat" }, NoValues, null, ["glm-5.3"]).Problem.ShouldNotBeNull().ShouldContain("openai-chat", Case.Sensitive);
    }

    [Fact]
    public void UserModelIdsAreAddedOnceAndBadOnesAreRefused()
    {
        var provider = Build("perplexity", "responses", models: ["sonar", " sonar ", "sonar-pro"]).Provider.ShouldNotBeNull();

        provider.Models.Select(model => model.Id).ShouldBe(["sonar", "sonar-pro"]);
        Build("perplexity", "responses", models: ["has space"]).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void ADocumentedModelIsListedBeforeTheUsersOwn()
    {
        var row = CatalogueRows.Find("scaleway")!;
        var documented = row.Routes[0].Models;
        documented.ShouldNotBeEmpty();

        var provider = Build("scaleway", "openai", models: [UserModel]).Provider.ShouldNotBeNull();

        provider.Models.Select(model => model.Id).ShouldBe([.. documented.Select(model => model.Id), UserModel]);
    }

    [Fact]
    public void NullArgumentsThrow()
    {
        var row = CatalogueRows.Find("scaleway")!;

        Should.Throw<ArgumentNullException>(() => OmpCatalogueBlock.Build(null!, row.Routes[0], NoValues));
        Should.Throw<ArgumentNullException>(() => OmpCatalogueBlock.Build(row, null!, NoValues));
        Should.Throw<ArgumentNullException>(() => OmpCatalogueBlock.Build(row, row.Routes[0], null!));
    }
}
