using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Providers.Tests;

/// <summary>
/// Confirms that each seed built from a VERIFIED research row passes the entry rules and reproduces the transport, auth
/// type, base endpoint and capability flags that its row states for the route the seed names. Every test cites its row
/// as file:line in docs/plans/vision-research/.
/// </summary>
public sealed class VerifiedProviderRowTests
{
    [Fact]
    public void OllamaLocalOpenAiRouteRowIsAccepted()
    {
        // providers-local-us.md:15 (Ollama, OpenAI-compatible local route), status verified.
        ProviderEntryValidator.Validate(ProviderCatalogue.OllamaLocalOpenAi).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void OllamaLocalOpenAiRouteMatchesRow()
    {
        // providers-local-us.md:15: local base http://localhost:11434/v1/; key required by client but ignored.
        var entry = ProviderCatalogue.OllamaLocalOpenAi;

        entry.Transport.ShouldBe(ProviderTransport.OpenAiCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.ApiKey);
        entry.BaseEndpoint.ShouldBe(new Uri("http://localhost:11434/v1/"));
        entry.Capabilities.ShouldBe(ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses);
    }

    [Fact]
    public void OllamaCloudOpenAiRouteRowIsAccepted()
    {
        // providers-local-us.md:15 (Ollama, OpenAI-compatible cloud route), status verified.
        ProviderEntryValidator.Validate(ProviderCatalogue.OllamaCloudOpenAi).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void OllamaCloudOpenAiRouteMatchesRow()
    {
        // providers-local-us.md:15: cloud base https://ollama.com/v1; Bearer OLLAMA_API_KEY.
        var entry = ProviderCatalogue.OllamaCloudOpenAi;

        entry.Transport.ShouldBe(ProviderTransport.OpenAiCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.BearerToken);
        entry.BaseEndpoint.ShouldBe(new Uri("https://ollama.com/v1"));
        entry.Capabilities.ShouldBe(ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses);
    }

    [Fact]
    public void LlamaCppLocalOpenAiRouteRowIsAccepted()
    {
        // providers-local-us.md:17 (llama.cpp server, OpenAI-compatible route), status verified.
        ProviderEntryValidator.Validate(ProviderCatalogue.LlamaCppLocalOpenAi).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void LlamaCppLocalOpenAiRouteMatchesRow()
    {
        // providers-local-us.md:17: default base http://127.0.0.1:9931; --api-key flag; chat, responses, embeddings.
        var entry = ProviderCatalogue.LlamaCppLocalOpenAi;

        entry.Transport.ShouldBe(ProviderTransport.OpenAiCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.ApiKey);
        entry.BaseEndpoint.ShouldBe(new Uri("http://127.0.0.1:9931"));
        entry.Capabilities.ShouldBe(ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses | ProviderCapabilities.Embeddings);
    }

    [Fact]
    public void PerplexityRowIsAccepted()
    {
        // providers-local-us.md:36 (Perplexity, Responses route), status verified.
        ProviderEntryValidator.Validate(ProviderCatalogue.Perplexity).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void PerplexityMatchesRow()
    {
        // providers-local-us.md:36: base https://api.perplexity.ai/v1; Responses format; API key.
        var entry = ProviderCatalogue.Perplexity;

        entry.Transport.ShouldBe(ProviderTransport.OpenAiCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.ApiKey);
        entry.BaseEndpoint.ShouldBe(new Uri("https://api.perplexity.ai/v1"));
        entry.Capabilities.ShouldBe(ProviderCapabilities.Responses);
    }

    [Fact]
    public void OpenRouterAnthropicRouteRowIsAccepted()
    {
        // providers-local-us.md:31 (OpenRouter, Anthropic route for Claude Code), status verified.
        ProviderEntryValidator.Validate(ProviderCatalogue.OpenRouterAnthropic).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void OpenRouterAnthropicRouteMatchesRow()
    {
        // providers-local-us.md:31: Claude Code base https://openrouter.ai/api; Bearer OPENROUTER_API_KEY.
        var entry = ProviderCatalogue.OpenRouterAnthropic;

        entry.Transport.ShouldBe(ProviderTransport.AnthropicCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.BearerToken);
        entry.BaseEndpoint.ShouldBe(new Uri("https://openrouter.ai/api"));
        entry.Capabilities.ShouldBe(ProviderCapabilities.None);
    }

    [Fact]
    public void HuggingFaceRouteRowIsAccepted()
    {
        // providers-eu-cn.md:19 (Hugging Face Inference Providers, EU-7, OpenAI-compatible route), status VERIFIED.
        ProviderEntryValidator.Validate(ProviderCatalogue.HuggingFaceRouter).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void HuggingFaceRouteMatchesRow()
    {
        // providers-eu-cn.md:19: OpenAI-compatible (V) base https://router.huggingface.co/v1; HF token, Bearer (V).
        var entry = ProviderCatalogue.HuggingFaceRouter;

        entry.Transport.ShouldBe(ProviderTransport.OpenAiCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.BearerToken);
        entry.BaseEndpoint.ShouldBe(new Uri("https://router.huggingface.co/v1"));
        entry.Capabilities.ShouldBe(ProviderCapabilities.None);
    }

    [Fact]
    public void DeepSeekAnthropicRouteRowIsAccepted()
    {
        // providers-eu-cn.md:22 (DeepSeek, CN-1, Anthropic route), status VERIFIED.
        ProviderEntryValidator.Validate(ProviderCatalogue.DeepSeekAnthropic).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void DeepSeekAnthropicRouteMatchesRow()
    {
        // providers-eu-cn.md:22: Anthropic base https://api.deepseek.com/anthropic (V); API key, Bearer (V).
        var entry = ProviderCatalogue.DeepSeekAnthropic;

        entry.Transport.ShouldBe(ProviderTransport.AnthropicCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.BearerToken);
        entry.BaseEndpoint.ShouldBe(new Uri("https://api.deepseek.com/anthropic"));
        entry.Capabilities.ShouldBe(ProviderCapabilities.None);
    }

    [Fact]
    public void MiniMaxAnthropicRouteRowIsAccepted()
    {
        // providers-eu-cn.md:26 (MiniMax M Plan, CN-5, Anthropic route), status VERIFIED.
        ProviderEntryValidator.Validate(ProviderCatalogue.MiniMaxAnthropic).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void MiniMaxAnthropicRouteMatchesRow()
    {
        // providers-eu-cn.md:26: Anthropic base https://api.minimax.io/anthropic (V); M Plan API key, header not stated.
        var entry = ProviderCatalogue.MiniMaxAnthropic;

        entry.Transport.ShouldBe(ProviderTransport.AnthropicCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.ApiKey);
        entry.BaseEndpoint.ShouldBe(new Uri("https://api.minimax.io/anthropic"));
        entry.Capabilities.ShouldBe(ProviderCapabilities.None);
    }
}
