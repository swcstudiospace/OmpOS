using Shouldly;
using Xunit;

namespace OmpOS.Harness.OmpConfig.Tests;

/// <summary>The thirteen <c>api</c> values omp's models schema accepts at provider level map to enum members and back, with the exact spelling omp reads.</summary>
public sealed class OmpProviderApiNamesTests
{
    /// <summary>The spellings of omp 18.12.6 (<c>models-config-schema-bundle.ts</c>, <c>ApiSchema</c>), written out independently of the code under test.</summary>
    private static readonly (OmpProviderApi Api, string Name)[] Pairs =
    [
        (OmpProviderApi.OpenAiCompletions, "openai-completions"),
        (OmpProviderApi.OpenAiResponses, "openai-responses"),
        (OmpProviderApi.AnthropicMessages, "anthropic-messages"),
        (OmpProviderApi.OpenAiCodexResponses, "openai-codex-responses"),
        (OmpProviderApi.AzureOpenAiResponses, "azure-openai-responses"),
        (OmpProviderApi.BedrockConverseStream, "bedrock-converse-stream"),
        (OmpProviderApi.GoogleGenerativeAi, "google-generative-ai"),
        (OmpProviderApi.GoogleGeminiCli, "google-gemini-cli"),
        (OmpProviderApi.GoogleVertex, "google-vertex"),
        (OmpProviderApi.Typesafe, "typesafe"),
        (OmpProviderApi.OpenRouterDecisions, "openrouter-decisions"),
        (OmpProviderApi.OpenAiDecisions, "openai-decisions"),
        (OmpProviderApi.WorkersAiSystemOne, "workers-ai-systemone"),
    ];

    public static TheoryData<OmpProviderApi, string> Spellings()
    {
        var data = new TheoryData<OmpProviderApi, string>();
        foreach (var (api, name) in Pairs)
        {
            data.Add(api, name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Spellings))]
    public void EveryValueRoundTrips(OmpProviderApi api, string name)
    {
        OmpProviderApiNames.ToYaml(api).ShouldBe(name);
        OmpProviderApiNames.TryParse(name, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(api);
    }

    [Fact]
    public void EveryEnumMemberHasASpelling() =>
        Pairs.Select(pair => pair.Api).Order().ShouldBe(Enum.GetValues<OmpProviderApi>());

    [Theory]
    [InlineData("")]
    [InlineData("openai")]
    [InlineData("OpenAI-Completions")]
    [InlineData("openai-chat")]
    [InlineData("bedrock")]
    public void ANameOmpDoesNotKnowIsRefused(string name) => OmpProviderApiNames.TryParse(name, out _).ShouldBeFalse();

    [Fact]
    public void AnUndefinedEnumValueHasNoName() => OmpProviderApiNames.ToYaml((OmpProviderApi)999).ShouldBeNull();

    [Fact]
    public void TheTenCatalogueApiValuesAreAllWritable()
    {
        foreach (var name in OmpOS.Harness.Providers.CatalogueApiNames.All)
        {
            OmpProviderApiNames.TryParse(name, out _).ShouldBeTrue(name);
        }
    }
}
