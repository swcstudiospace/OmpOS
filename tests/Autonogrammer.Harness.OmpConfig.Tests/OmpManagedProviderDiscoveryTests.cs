using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

/// <summary>The discovery, key header and Bearer header members of a managed provider: what the validator accepts and what the reader refuses in a hand-edited block.</summary>
public sealed class OmpManagedProviderDiscoveryTests
{
    private static readonly OmpManagedDiscovery Discovery = new(OmpManagedDiscovery.OpenAiModelsList);

    private static OmpManagedProvider Discovered() =>
        new("vendor", new Uri("https://api.vendor.test/v1"), OmpProviderApi.OpenAiCompletions, "VENDOR_API_KEY", NoAuthentication: false, [], Discovery: Discovery);

    private static bool Parse(params string[] inner) => OmpManagedBlockReader.TryParse("vendor", [.. inner], out _, out _);

    private static string ProblemOf(params string[] inner)
    {
        OmpManagedBlockReader.TryParse("vendor", [.. inner], out _, out var problem).ShouldBeFalse();
        return problem;
    }

    [Fact]
    public void DiscoveryReplacesTheNeedForAModel() => OmpManagedProviderValidator.Validate(Discovered()).ShouldBeNull();

    [Fact]
    public void WithoutDiscoveryAModelIsStillRequired() =>
        OmpManagedProviderValidator.Validate(Discovered() with { Discovery = null }).ShouldNotBeNull();

    [Theory]
    [InlineData("")]
    [InlineData("openai")]
    [InlineData("models-list")]
    public void AnUnknownDiscoveryTypeIsRefused(string type) =>
        OmpManagedProviderValidator.Validate(Discovered() with { Discovery = new OmpManagedDiscovery(type) }).ShouldNotBeNull();

    [Theory]
    [InlineData("ollama")]
    [InlineData("llama.cpp")]
    [InlineData("lm-studio")]
    [InlineData("openai-models-list")]
    [InlineData("proxy")]
    [InlineData("litellm")]
    [InlineData("apple-foundation-models")]
    public void EveryDiscoveryTypeOmpKnowsIsAccepted(string type) =>
        OmpManagedProviderValidator.Validate(Discovered() with { Discovery = new OmpManagedDiscovery(type) }).ShouldBeNull();

    [Fact]
    public void AKeyHeaderNeedsAKeyAndAValidName()
    {
        OmpManagedProviderValidator.Validate(Discovered() with { KeyHeaderName = "X-API-Key" }).ShouldBeNull();
        OmpManagedProviderValidator.Validate(Discovered() with { KeyHeaderName = "bad name" }).ShouldNotBeNull();
        OmpManagedProviderValidator.Validate(Discovered() with { KeyHeaderName = "" }).ShouldNotBeNull();
        OmpManagedProviderValidator.Validate(Discovered() with { ApiKeyEnvName = null, NoAuthentication = true, KeyHeaderName = "X-API-Key" }).ShouldNotBeNull();
    }

    [Fact]
    public void ABearerHeaderNeedsAKey()
    {
        OmpManagedProviderValidator.Validate(Discovered() with { BearerAuthHeader = true }).ShouldBeNull();
        OmpManagedProviderValidator.Validate(Discovered() with { ApiKeyEnvName = null, NoAuthentication = true, BearerAuthHeader = true }).ShouldNotBeNull();
    }

    [Fact]
    public void AHeaderWhoseValueIsNotTheKeySettingIsRefusedWhenReading() =>
        ProblemOf(
            "\"vendor\":",
            "  baseUrl: \"https://api.vendor.test/v1\"",
            "  api: openai-completions",
            "  apiKey: \"VENDOR_API_KEY\"",
            "  headers:",
            "    \"X-API-Key\": \"sk-literal-value\"",
            "  discovery:",
            "    type: \"openai-models-list\"").ShouldContain("header value", Case.Sensitive);

    [Fact]
    public void AMemberInsideDiscoveryThatAutonogrammerDoesNotWriteIsRefusedWhenReading() =>
        ProblemOf(
            "\"vendor\":",
            "  baseUrl: \"https://api.vendor.test/v1\"",
            "  api: openai-completions",
            "  apiKey: \"VENDOR_API_KEY\"",
            "  discovery:",
            "    type: \"openai-models-list\"",
            "    injectV1: false").ShouldContain("injectV1", Case.Sensitive);

    [Fact]
    public void ATwoHeaderBlockIsRefusedWhenReading() =>
        Parse(
            "\"vendor\":",
            "  baseUrl: \"https://api.vendor.test/v1\"",
            "  api: openai-completions",
            "  apiKey: \"VENDOR_API_KEY\"",
            "  headers:",
            "    \"X-A\": \"VENDOR_API_KEY\"",
            "    \"X-B\": \"VENDOR_API_KEY\"",
            "  discovery:",
            "    type: \"openai-models-list\"").ShouldBeFalse();

    [Fact]
    public void ABadAuthHeaderValueIsRefusedWhenReading() =>
        ProblemOf(
            "\"vendor\":",
            "  baseUrl: \"https://api.vendor.test/v1\"",
            "  api: openai-completions",
            "  apiKey: \"VENDOR_API_KEY\"",
            "  authHeader: yes",
            "  discovery:",
            "    type: \"openai-models-list\"").ShouldContain("authHeader", Case.Sensitive);

    [Fact]
    public void ADiscoveryBlockAfterTheModelsListIsRefusedWhenReading() =>
        Parse(
            "\"vendor\":",
            "  baseUrl: \"https://api.vendor.test/v1\"",
            "  api: openai-completions",
            "  apiKey: \"VENDOR_API_KEY\"",
            "  models:",
            "    - id: \"m\"",
            "  discovery:",
            "    type: \"openai-models-list\"").ShouldBeFalse();

    [Fact]
    public void ABlockWithDiscoveryAndNoModelsIsReadBack() =>
        Parse(
            "\"vendor\":",
            "  baseUrl: \"https://api.vendor.test/v1\"",
            "  api: openai-completions",
            "  apiKey: \"VENDOR_API_KEY\"",
            "  discovery:",
            "    type: \"openai-models-list\"").ShouldBeTrue();

    [Fact]
    public void ABlockWithNeitherModelsNorDiscoveryIsRefusedWhenReading() =>
        Parse(
            "\"vendor\":",
            "  baseUrl: \"https://api.vendor.test/v1\"",
            "  api: openai-completions",
            "  apiKey: \"VENDOR_API_KEY\"").ShouldBeFalse();
}
