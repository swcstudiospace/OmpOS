using Xunit;

namespace OmpOS.Harness.OmpConfig.Tests;

/// <summary>Providers and <c>models.yml</c> texts shared by the editor and file tests.</summary>
internal static class OmpModelsFixtures
{
    /// <summary>A keyless local provider with one model.</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>The provider.</returns>
    public static OmpManagedProvider Local(string id = "acme") =>
        new(id, new Uri("http://127.0.0.1:11434/v1"), OmpProviderApi.OpenAiCompletions, ApiKeyEnvName: null, NoAuthentication: true, [new OmpManagedModel("small")]);

    /// <summary>A keyed Anthropic-protocol provider that uses every optional field and ids that need quoting.</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>The provider.</returns>
    public static OmpManagedProvider Keyed(string id = "gateway") =>
        new(
            id,
            new Uri("https://llm.example.com/anthropic"),
            OmpProviderApi.AnthropicMessages,
            ApiKeyEnvName: "GATEWAY_API_KEY",
            NoAuthentication: false,
            [
                new OmpManagedModel("claude-sonnet", "Claude \"Sonnet\" \\ ünï", Reasoning: true, AcceptsImages: true, ContextWindow: 200000, MaxTokens: 64000),
                new OmpManagedModel("llama3:8b"),
                new OmpManagedModel("123"),
                new OmpManagedModel("true", "yes"),
            ]);

    /// <summary>Text files that exercise the byte-for-byte guarantee, as name and content.</summary>
    /// <returns>The fixtures.</returns>
    public static TheoryData<string, string> Texts() => new()
    {
        { "empty file", string.Empty },
        { "single newline", "\n" },
        { "no providers key", "# comment only\nother: 1\n" },
        { "no providers key and no final newline", "other: 1" },
        { "similar root key", "providers2:\n  a: 1\n" },
        { "two-space children", "providers:\n  a:\n    baseUrl: x\n    models:\n      - id: m\n" },
        { "four-space children", "providers:\n    a:\n        baseUrl: x\n        models:\n            - id: m\n" },
        { "three-space children", "providers:\n   a:\n      baseUrl: x\n" },
        { "crlf line endings", "# c\r\nproviders:\r\n  a:\r\n    baseUrl: x\r\n" },
        { "crlf without final newline", "providers:\r\n  a:\r\n    x: 1" },
        { "comments before and after", "# header\nproviders:\n  a:\n    baseUrl: x\n  # trailing child comment\n# about other\nother: 1\n# footer\n" },
        { "no final newline", "providers:\n  a:\n    baseUrl: x" },
        { "empty providers mapping", "providers:\n" },
        { "empty providers mapping without newline", "providers:" },
        { "providers with a comment", "providers: # my providers\n  a:\n    x: 1\n" },
        { "trailing blank lines", "providers:\n  a:\n    x: 1\n\n\n" },
        { "byte order mark", "\uFEFFproviders:\n  a:\n    x: 1\n" },
        { "keys before and after providers", "model: x\nproviders:\n  a:\n    x: 1\nmodelRoles:\n  default: a/b\n" },
        { "quoted providers key", "\"providers\":\n  a:\n    x: 1\n" },
        { "blank line inside providers", "providers:\n\nother: 1\n" },
        { "nested key named like the new id", "providers:\n  other:\n    acme: 1\n" },
        { "trailing spaces and unicode", "providers:   \n  ä:  \n    x: 'ünï'  \n" },
    };
}
