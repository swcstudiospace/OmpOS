using Shouldly;
using Xunit;

namespace OmpOS.Harness.Providers.Tests;

/// <summary>Verifies whole-entry rules: the id slug, the citation fields, the endpoint rules, and reporting every failure together.</summary>
public sealed class ProviderEntryValidatorTests
{
    [Theory]
    [InlineData("ollama-local")]
    [InlineData("a")]
    [InlineData("llama-cpp-2")]
    public void WellFormedIdsAreAccepted(string id)
    {
        var result = ProviderEntryValidator.Validate(ProviderCatalogue.OllamaLocalOpenAi with { Id = id });

        result.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ollama")]
    [InlineData("ollama local")]
    [InlineData("ollama_local")]
    [InlineData("-ollama")]
    [InlineData("ollama-")]
    [InlineData("ollama\n")]
    public void MalformedIdsAreRejected(string id)
    {
        var result = ProviderEntryValidator.Validate(ProviderCatalogue.OllamaLocalOpenAi with { Id = id });

        result.Errors.ShouldContain(ProviderValidationError.InvalidId);
    }

    [Fact]
    public void IdOfSixtyFourCharactersIsAccepted()
    {
        var result = ProviderEntryValidator.Validate(ProviderCatalogue.OllamaLocalOpenAi with { Id = new string('a', 64) });

        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void IdOfSixtyFiveCharactersIsRejected()
    {
        var result = ProviderEntryValidator.Validate(ProviderCatalogue.OllamaLocalOpenAi with { Id = new string('a', 65) });

        result.Errors.ShouldContain(ProviderValidationError.InvalidId);
    }

    [Fact]
    public void MissingSourceUrlIsRejected()
    {
        // Row: providers-eu-cn.md:22 (DeepSeek), with the source citation removed.
        var result = ProviderEntryValidator.Validate(ProviderCatalogue.DeepSeekAnthropic with { SourceUrl = null });

        result.Errors.ShouldContain(ProviderValidationError.MissingSourceUrl);
    }

    [Fact]
    public void MissingRetrievedOnIsRejected()
    {
        // Row: providers-eu-cn.md:19 (Hugging Face), with the retrieval date left at its default value.
        var result = ProviderEntryValidator.Validate(ProviderCatalogue.HuggingFaceRouter with { RetrievedOn = default });

        result.Errors.ShouldContain(ProviderValidationError.MissingRetrievedOn);
    }

    [Fact]
    public void CredentialInBaseEndpointIsRejected()
    {
        // Row: providers-local-us.md:15 (Ollama cloud base https://ollama.com/v1), with a key placed in the query string.
        var entry = ProviderCatalogue.OllamaCloudOpenAi with { BaseEndpoint = new Uri("https://ollama.com/v1?api_key=secret") };

        var result = ProviderEntryValidator.Validate(entry);

        result.Errors.ShouldContain(ProviderValidationError.CredentialInEndpoint);
    }

    [Fact]
    public void EveryBrokenRuleIsReportedTogether()
    {
        // Row: providers-local-us.md:15 (Ollama local), with both citation fields missing.
        var entry = ProviderCatalogue.OllamaLocalOpenAi with { SourceUrl = null, RetrievedOn = default };

        var result = ProviderEntryValidator.Validate(entry);

        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldContain(ProviderValidationError.MissingSourceUrl);
        result.Errors.ShouldContain(ProviderValidationError.MissingRetrievedOn);
    }

    [Fact]
    public void AnAuthHeaderNameOnAnEntryThatSendsNoCredentialIsRejected()
    {
        // Row: providers-local-us.md:16 (LM Studio, auth none by default), with a header name set although no credential is sent.
        var entry = ProviderCatalogue.LmStudioLocalOpenAi with { AuthHeaderName = "x-api-key" };

        var result = ProviderEntryValidator.Validate(entry);

        result.Errors.ShouldBe([ProviderValidationError.AuthHeaderWithoutCredential]);
    }

    [Fact]
    public void AWellFormedAuthHeaderNameOnACredentialedEntryIsAccepted()
    {
        // Row: providers-local-us.md:15 (Ollama local, key required by the client), carried in an x-api-key header.
        var entry = ProviderCatalogue.OllamaLocalOpenAi with { AuthHeaderName = "x-api-key" };

        ProviderEntryValidator.Validate(entry).Errors.ShouldBeEmpty();
    }
}
