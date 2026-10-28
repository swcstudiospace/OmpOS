using Autonogrammer.Presentation.Settings.Models;

namespace Autonogrammer.Native.Tests.Settings.Models;

/// <summary>Custom provider forms and the canary key shared by the Models source tests.</summary>
internal static class ModelsForms
{
    /// <summary>A key value that no output may contain. It is a test value, not a credential.</summary>
    public const string Canary = "sk-canary-autonogrammer-key-4f19c2d8e7a35b61";

    /// <summary>A keyless OpenAI-compatible provider on a loopback address.</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>The form.</returns>
    public static CustomProviderForm Local(string id) =>
        new(
            "Local " + id,
            id,
            CustomProviderProtocol.OpenAiCompatible,
            new Uri("http://127.0.0.1:11434/v1"),
            CustomProviderAuth.None,
            ["small"],
            CustomProviderCapabilities.Chat,
            AllowRemotePlainHttp: false);

    /// <summary>A keyed Anthropic-compatible provider with a Bearer key and two models.</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>The form.</returns>
    public static CustomProviderForm Gateway(string id) =>
        new(
            "Acme Gateway",
            id,
            CustomProviderProtocol.AnthropicCompatible,
            new Uri("https://llm.example.com/anthropic"),
            CustomProviderAuth.Bearer,
            ["claude-sonnet", "llama3:8b"],
            CustomProviderCapabilities.Chat | CustomProviderCapabilities.Embeddings,
            AllowRemotePlainHttp: false);

    /// <summary>A keyed OpenAI-compatible provider whose endpoint speaks the Responses route only.</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>The form.</returns>
    public static CustomProviderForm ResponsesOnly(string id) =>
        Gateway(id) with
        {
            Protocol = CustomProviderProtocol.OpenAiCompatible,
            Auth = CustomProviderAuth.ApiKey,
            BaseEndpoint = new Uri("https://api.example.com/v1"),
            Capabilities = CustomProviderCapabilities.Responses,
            Models = ["gpt-x"],
        };
}
