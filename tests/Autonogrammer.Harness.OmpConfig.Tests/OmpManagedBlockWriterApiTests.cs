using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

/// <summary>
/// Golden blocks: the writer emits the exact text omp's schema reads for every api style, with the key as an environment variable NAME or
/// <c>auth: none</c>, and the reader reads each block back to an equal provider. The templates are written out here, not derived from the writer.
/// </summary>
public sealed class OmpManagedBlockWriterApiTests
{
    private static readonly OmpManagedModel[] Documented = [new OmpManagedModel("m-1")];

    private static string Render(OmpManagedProvider provider) => string.Join('\n', OmpManagedBlockWriter.Render(provider, 2, 2));

    private static OmpManagedProvider Keyed(OmpProviderApi api) =>
        new("vendor", new Uri("https://api.vendor.test/v1"), api, "VENDOR_API_KEY", NoAuthentication: false, Documented);

    private static string KeyedGolden(string api) =>
        "  # autonogrammer:begin vendor\n" +
        "  \"vendor\":\n" +
        "    baseUrl: \"https://api.vendor.test/v1\"\n" +
        $"    api: {api}\n" +
        "    apiKey: \"VENDOR_API_KEY\"\n" +
        "    models:\n" +
        "      - id: \"m-1\"\n" +
        "        reasoning: false\n" +
        "        input: [text]\n" +
        "  # autonogrammer:end vendor";

    [Theory]
    [MemberData(nameof(NonAnthropicStyles))]
    public void EveryNonAnthropicStyleWritesAKeyedBlockWithTheKeyAsAnEnvName(OmpProviderApi api, string name)
    {
        var provider = Keyed(api);

        Render(provider).ShouldBe(KeyedGolden(name));
        Render(provider).ShouldNotContain("auth:", Case.Sensitive);
    }

    public static TheoryData<OmpProviderApi, string> NonAnthropicStyles() => new()
    {
        { OmpProviderApi.OpenAiCompletions, "openai-completions" },
        { OmpProviderApi.OpenAiResponses, "openai-responses" },
        { OmpProviderApi.OpenAiCodexResponses, "openai-codex-responses" },
        { OmpProviderApi.AzureOpenAiResponses, "azure-openai-responses" },
        { OmpProviderApi.BedrockConverseStream, "bedrock-converse-stream" },
        { OmpProviderApi.GoogleGenerativeAi, "google-generative-ai" },
        { OmpProviderApi.GoogleGeminiCli, "google-gemini-cli" },
        { OmpProviderApi.GoogleVertex, "google-vertex" },
        { OmpProviderApi.Typesafe, "typesafe" },
        { OmpProviderApi.OpenRouterDecisions, "openrouter-decisions" },
        { OmpProviderApi.OpenAiDecisions, "openai-decisions" },
        { OmpProviderApi.WorkersAiSystemOne, "workers-ai-systemone" },
    };

    [Fact]
    public void AnAnthropicKeyedBlockAddsThePlainAuthShape() =>
        Render(Keyed(OmpProviderApi.AnthropicMessages)).ShouldBe(
            "  # autonogrammer:begin vendor\n" +
            "  \"vendor\":\n" +
            "    baseUrl: \"https://api.vendor.test/v1\"\n" +
            "    api: anthropic-messages\n" +
            "    apiKey: \"VENDOR_API_KEY\"\n" +
            "    auth: apiKey\n" +
            "    models:\n" +
            "      - id: \"m-1\"\n" +
            "        reasoning: false\n" +
            "        input: [text]\n" +
            "  # autonogrammer:end vendor");

    [Fact]
    public void ABearerAnthropicBlockAlsoAsksOmpToSendTheBearerHeader() =>
        Render(Keyed(OmpProviderApi.AnthropicMessages) with { BearerAuthHeader = true }).ShouldBe(
            "  # autonogrammer:begin vendor\n" +
            "  \"vendor\":\n" +
            "    baseUrl: \"https://api.vendor.test/v1\"\n" +
            "    api: anthropic-messages\n" +
            "    apiKey: \"VENDOR_API_KEY\"\n" +
            "    auth: apiKey\n" +
            "    authHeader: true\n" +
            "    models:\n" +
            "      - id: \"m-1\"\n" +
            "        reasoning: false\n" +
            "        input: [text]\n" +
            "  # autonogrammer:end vendor");

    [Fact]
    public void AKeylessLocalBlockWritesAuthNoneAndDiscovery()
    {
        var provider = new OmpManagedProvider(
            "local",
            new Uri("http://localhost:1234/v1"),
            OmpProviderApi.OpenAiCompletions,
            ApiKeyEnvName: null,
            NoAuthentication: true,
            [],
            Discovery: new OmpManagedDiscovery(OmpManagedDiscovery.OpenAiModelsList));

        Render(provider).ShouldBe(
            "  # autonogrammer:begin local\n" +
            "  \"local\":\n" +
            "    baseUrl: \"http://localhost:1234/v1\"\n" +
            "    api: openai-completions\n" +
            "    auth: none\n" +
            "    discovery:\n" +
            "      type: \"openai-models-list\"\n" +
            "  # autonogrammer:end local");
    }

    [Fact]
    public void ACustomKeyHeaderCarriesTheEnvNameNeverAKeyValue()
    {
        var provider = Keyed(OmpProviderApi.OpenAiCompletions) with
        {
            KeyHeaderName = "X-API-Key",
            Discovery = new OmpManagedDiscovery(OmpManagedDiscovery.OpenAiModelsList),
        };

        Render(provider).ShouldBe(
            "  # autonogrammer:begin vendor\n" +
            "  \"vendor\":\n" +
            "    baseUrl: \"https://api.vendor.test/v1\"\n" +
            "    api: openai-completions\n" +
            "    apiKey: \"VENDOR_API_KEY\"\n" +
            "    headers:\n" +
            "      \"X-API-Key\": \"VENDOR_API_KEY\"\n" +
            "    discovery:\n" +
            "      type: \"openai-models-list\"\n" +
            "    models:\n" +
            "      - id: \"m-1\"\n" +
            "        reasoning: false\n" +
            "        input: [text]\n" +
            "  # autonogrammer:end vendor");
    }

    [Fact]
    public void FourSpaceIndentationIsHonoured()
    {
        var provider = Keyed(OmpProviderApi.OpenAiCompletions) with { Discovery = new OmpManagedDiscovery(OmpManagedDiscovery.OpenAiModelsList) };

        var lines = OmpManagedBlockWriter.Render(provider, 4, 4);

        lines.ShouldContain("        discovery:", StringComparer.Ordinal);
        lines.ShouldContain("            type: \"openai-models-list\"", StringComparer.Ordinal);
        lines.ShouldContain("        models:", StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AllProviders))]
    public void EveryWrittenBlockIsReadBackToAnEqualProvider(string label, OmpManagedProvider provider)
    {
        var lines = OmpManagedBlockWriter.Render(provider, 2, 2);
        var inner = lines.Skip(1).Take(lines.Count - 2).ToList();

        OmpManagedBlockReader.TryParse(provider.Id, inner, out var parsed, out var problem).ShouldBeTrue($"{label}: {problem}");

        var read = parsed.ShouldNotBeNull();
        read.BaseUrl.OriginalString.ShouldBe(provider.BaseUrl.OriginalString, label);
        read.Api.ShouldBe(provider.Api, label);
        read.ApiKeyEnvName.ShouldBe(provider.ApiKeyEnvName, label);
        read.NoAuthentication.ShouldBe(provider.NoAuthentication, label);
        read.BearerAuthHeader.ShouldBe(provider.BearerAuthHeader, label);
        read.KeyHeaderName.ShouldBe(provider.KeyHeaderName, label);
        read.Discovery.ShouldBe(provider.Discovery, label);
        read.Models.Select(model => model.Id).ShouldBe(provider.Models.Select(model => model.Id), label);
    }

    public static TheoryData<string, OmpManagedProvider> AllProviders()
    {
        var data = new TheoryData<string, OmpManagedProvider>();
        foreach (var api in Enum.GetValues<OmpProviderApi>())
        {
            data.Add($"keyed {api}", Keyed(api));
        }

        data.Add("bearer anthropic", Keyed(OmpProviderApi.AnthropicMessages) with { BearerAuthHeader = true });
        data.Add("custom header with discovery", Keyed(OmpProviderApi.OpenAiCompletions) with
        {
            KeyHeaderName = "X-API-Key",
            Discovery = new OmpManagedDiscovery(OmpManagedDiscovery.OpenAiModelsList),
        });
        data.Add("keyless with discovery", new OmpManagedProvider(
            "local", new Uri("http://127.0.0.1:8080/v1"), OmpProviderApi.OpenAiResponses, null, NoAuthentication: true, [],
            Discovery: new OmpManagedDiscovery(OmpManagedDiscovery.OpenAiModelsList)));
        return data;
    }
}
