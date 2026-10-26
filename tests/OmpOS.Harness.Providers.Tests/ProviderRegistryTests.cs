using System.Text.Json;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Providers.Tests;

/// <summary>
/// Defines custom providers from user data alone and checks that they validate like catalogue rows, persist, and reload
/// unchanged. Every test uses its own temporary directory.
/// </summary>
public sealed class ProviderRegistryTests
{
    private static CancellationToken NoCancellation => TestContext.Current.CancellationToken;

    private static CustomProviderDefinition Gateway(string id = "team-gateway") => new(
        id,
        "Team gateway",
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.ApiKey,
        "x-api-key",
        new Uri("http://localhost:4000/v1"),
        ["qwen3-coder", "meta-llama/Llama-3.3-70B-Instruct"],
        ProviderCapabilities.ChatCompletions | ProviderCapabilities.Embeddings);

    [Fact]
    public async Task CustomProviderIsAddedFromUserEntryWithoutCodeChange()
    {
        using var directory = new TempDirectory();
        using var registry = await OpenAsync(directory).ConfigureAwait(true);

        var result = await registry.AddCustomAsync(Gateway(), NoCancellation).ConfigureAwait(true);

        result.Errors.ShouldBeEmpty();
        var entry = registry.Find("team-gateway").ShouldNotBeNull();
        entry.Kind.ShouldBe(ProviderKind.Custom);
        entry.Transport.ShouldBe(ProviderTransport.OpenAiCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.ApiKey);
        entry.AuthHeaderName.ShouldBe("x-api-key");
        entry.BaseEndpoint.ShouldBe(new Uri("http://localhost:4000/v1"));
        entry.Capabilities.ShouldBe(ProviderCapabilities.ChatCompletions | ProviderCapabilities.Embeddings);
        entry.SourceUrl.ShouldBe(ProviderCatalogue.GenericOpenAiCompatible.SourceUrl);
        entry.RetrievedOn.ShouldBe(ProviderCatalogue.GenericOpenAiCompatible.RetrievedOn);
        ProviderEntryValidator.Validate(entry).Errors.ShouldBeEmpty();
        registry.ModelsFor("team-gateway").ShouldBe(["qwen3-coder", "meta-llama/Llama-3.3-70B-Instruct"]);
        registry.Entries.Select(candidate => candidate.Id).ShouldContain("team-gateway", StringComparer.Ordinal);
        registry.Entries.Take(ProviderCatalogue.All.Count).ShouldBe(ProviderCatalogue.All);
    }

    [Fact]
    public async Task AnAnthropicCompatibleDefinitionCitesTheAnthropicPattern()
    {
        using var directory = new TempDirectory();
        using var registry = await OpenAsync(directory).ConfigureAwait(true);
        var definition = Gateway("claude-proxy") with { Transport = ProviderTransport.AnthropicCompatible, Capabilities = ProviderCapabilities.None };

        (await registry.AddCustomAsync(definition, NoCancellation).ConfigureAwait(true)).IsValid.ShouldBeTrue();

        registry.Find("claude-proxy").ShouldNotBeNull().SourceUrl.ShouldBe(ProviderCatalogue.GenericAnthropicCompatible.SourceUrl);
    }

    [Fact]
    public async Task InvalidCustomProviderIsRejectedWithEveryError()
    {
        using var directory = new TempDirectory();
        var store = new ProviderRegistryStore(Path.Combine(directory.Path, "providers.json"));
        using var registry = await ProviderRegistry.OpenAsync(store, NoCancellation).ConfigureAwait(true);
        var broken = new CustomProviderDefinition(
            "Bad Id",
            "   ",
            ProviderTransport.OpenAiCompatible,
            ProviderAuthType.None,
            "Bad Header",
            new Uri("https://user:secret@example.test/v1?key=abc"),
            [" ", "a", "a"],
            ProviderCapabilities.ChatCompletions);

        var result = await registry.AddCustomAsync(broken, NoCancellation).ConfigureAwait(true);

        result.Errors.ShouldBe(
        [
            ProviderValidationError.MissingDisplayName,
            ProviderValidationError.InvalidId,
            ProviderValidationError.CredentialInEndpoint,
            ProviderValidationError.InvalidAuthHeaderName,
            ProviderValidationError.AuthHeaderWithoutCredential,
            ProviderValidationError.InvalidModelId,
            ProviderValidationError.DuplicateModelId,
        ]);
        registry.CustomProviders.ShouldBeEmpty();
        File.Exists(store.FilePath).ShouldBeFalse();
    }

    [Fact]
    public async Task PlainHttpToARemoteHostNeedsTheExplicitOptIn()
    {
        using var directory = new TempDirectory();
        using var registry = await OpenAsync(directory).ConfigureAwait(true);
        var remote = Gateway() with { BaseEndpoint = new Uri("http://llm.internal.example.test/v1") };

        (await registry.AddCustomAsync(remote, NoCancellation).ConfigureAwait(true)).Errors
            .ShouldBe([ProviderValidationError.PlainHttpToRemoteHost]);
        (await registry.AddCustomAsync(remote with { AllowRemotePlainHttp = true }, NoCancellation).ConfigureAwait(true)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task AnIdThatIsTakenIsRejectedWithoutChangingTheRegistry()
    {
        using var directory = new TempDirectory();
        using var registry = await OpenAsync(directory).ConfigureAwait(true);
        (await registry.AddCustomAsync(Gateway(), NoCancellation).ConfigureAwait(true)).IsValid.ShouldBeTrue();

        var again = await registry.AddCustomAsync(Gateway() with { DisplayName = "Other" }, NoCancellation).ConfigureAwait(true);
        var shadowing = await registry.AddCustomAsync(Gateway("ollama-local"), NoCancellation).ConfigureAwait(true);

        again.Errors.ShouldBe([ProviderValidationError.IdAlreadyRegistered]);
        shadowing.Errors.ShouldBe([ProviderValidationError.IdAlreadyRegistered]);
        registry.CustomProviders.Select(definition => definition.DisplayName).ShouldBe(["Team gateway"]);
        registry.Find("ollama-local").ShouldBeSameAs(ProviderCatalogue.OllamaLocalOpenAi);
    }

    [Fact]
    public async Task RegistryPersistsAcrossReopen()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nested", "providers.json");
        var first = Gateway("zeta-gateway");
        var second = new CustomProviderDefinition(
            "alpha-claude",
            "Alpha Claude",
            ProviderTransport.AnthropicCompatible,
            ProviderAuthType.BearerToken,
            null,
            new Uri("https://claude.proxy.example.test"),
            [],
            ProviderCapabilities.None);

        using (var registry = await ProviderRegistry.OpenAsync(new ProviderRegistryStore(path), NoCancellation).ConfigureAwait(true))
        {
            (await registry.AddCustomAsync(first, NoCancellation).ConfigureAwait(true)).IsValid.ShouldBeTrue();
            (await registry.AddCustomAsync(second, NoCancellation).ConfigureAwait(true)).IsValid.ShouldBeTrue();
        }

        using var reopened = await ProviderRegistry.OpenAsync(new ProviderRegistryStore(path), NoCancellation).ConfigureAwait(true);
        var providers = reopened.CustomProviders;
        providers.Select(definition => definition.Id).ShouldBe(["alpha-claude", "zeta-gateway"]);
        AssertSame(providers[0], second);
        AssertSame(providers[1], first);
        Directory.GetFiles(Path.GetDirectoryName(path)!).ShouldBe([path]);
    }

    [Fact]
    public async Task ARemovedProviderStaysRemovedAfterReopen()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "providers.json");
        using (var registry = await ProviderRegistry.OpenAsync(new ProviderRegistryStore(path), NoCancellation).ConfigureAwait(true))
        {
            await registry.AddCustomAsync(Gateway(), NoCancellation).ConfigureAwait(true);
            (await registry.RemoveCustomAsync("team-gateway", NoCancellation).ConfigureAwait(true)).ShouldBeTrue();
            (await registry.RemoveCustomAsync("team-gateway", NoCancellation).ConfigureAwait(true)).ShouldBeFalse();
            (await registry.RemoveCustomAsync("ollama-local", NoCancellation).ConfigureAwait(true)).ShouldBeFalse();
        }

        using var reopened = await ProviderRegistry.OpenAsync(new ProviderRegistryStore(path), NoCancellation).ConfigureAwait(true);
        reopened.CustomProviders.ShouldBeEmpty();
        reopened.Find("ollama-local").ShouldNotBeNull();
    }

    [Fact]
    public async Task TheStoreFileIsReadableByItsOwnerOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
        }

        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "providers.json");
        using var registry = await ProviderRegistry.OpenAsync(new ProviderRegistryStore(path), NoCancellation).ConfigureAwait(true);

        await registry.AddCustomAsync(Gateway(), NoCancellation).ConfigureAwait(true);

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"version":2,"providers":[]}""")]
    [InlineData("""{"version":1}""")]
    [InlineData("""{"version":1,"providers":[],"extra":1}""")]
    [InlineData("""{"version":1,"providers":[{"id":"a","displayName":"A","transport":"Carrier","authType":"None","baseEndpoint":"http://localhost:1/v1","models":[],"capabilities":[],"allowRemotePlainHttp":false}]}""")]
    [InlineData("""{"version":1,"providers":[{"id":"a","displayName":"A","transport":"1","authType":"None","baseEndpoint":"http://localhost:1/v1","models":[],"capabilities":[],"allowRemotePlainHttp":false}]}""")]
    [InlineData("""{"version":1,"providers":[{"id":"a","displayName":"A","transport":"OpenAiCompatible","authType":"None","baseEndpoint":"http://localhost:1/v1","models":[],"capabilities":["Telepathy"],"allowRemotePlainHttp":false}]}""")]
    [InlineData("""{"version":1,"providers":[{"id":"a","displayName":"A","transport":"OpenAiCompatible","authType":"None","baseEndpoint":"relative/path","models":[],"capabilities":[],"allowRemotePlainHttp":false}]}""")]
    [InlineData("""{"version":1,"providers":[{"id":"Bad Id","displayName":"A","transport":"OpenAiCompatible","authType":"None","baseEndpoint":"http://localhost:1/v1","models":[],"capabilities":[],"allowRemotePlainHttp":false}]}""")]
    [InlineData("""{"version":1,"providers":[{"id":"ollama-local","displayName":"A","transport":"OpenAiCompatible","authType":"None","baseEndpoint":"http://localhost:1/v1","models":[],"capabilities":[],"allowRemotePlainHttp":false}]}""")]
    public async Task AnUnreadableOrInvalidFileRefusesToOpenAndIsLeftAlone(string contents)
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "providers.json");
        await File.WriteAllTextAsync(path, contents, NoCancellation).ConfigureAwait(true);

        await Should.ThrowAsync<JsonException>(async () =>
            await ProviderRegistry.OpenAsync(new ProviderRegistryStore(path), NoCancellation).ConfigureAwait(true)).ConfigureAwait(true);

        (await File.ReadAllTextAsync(path, NoCancellation).ConfigureAwait(true)).ShouldBe(contents);
    }

    [Theory]
    [InlineData("x-api-key", true)]
    [InlineData("Authorization", true)]
    [InlineData("api_key.v2", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("colon:", false)]
    [InlineData("tab\t", false)]
    public void AuthHeaderNamesMustBeHttpTokens(string header, bool valid)
    {
        var result = (Gateway() with { AuthHeaderName = header }).Validate();

        result.Errors.Contains(ProviderValidationError.InvalidAuthHeaderName).ShouldBe(!valid);
    }

    [Fact]
    public void ModelIdsThatAreTooLongOrHoldWhitespaceAreRejected()
    {
        var longId = new string('m', CustomProviderDefinition.MaxModelIdLength + 1);
        var exactId = new string('m', CustomProviderDefinition.MaxModelIdLength);

        (Gateway() with { Models = [longId] }).Validate().Errors.ShouldBe([ProviderValidationError.InvalidModelId]);
        (Gateway() with { Models = ["two words"] }).Validate().Errors.ShouldBe([ProviderValidationError.InvalidModelId]);
        (Gateway() with { Models = [exactId] }).Validate().Errors.ShouldBeEmpty();
    }

    private static async Task<ProviderRegistry> OpenAsync(TempDirectory directory) =>
        await ProviderRegistry.OpenAsync(new ProviderRegistryStore(Path.Combine(directory.Path, "providers.json")), NoCancellation).ConfigureAwait(true);

    private static void AssertSame(CustomProviderDefinition actual, CustomProviderDefinition expected)
    {
        actual.Id.ShouldBe(expected.Id);
        actual.DisplayName.ShouldBe(expected.DisplayName);
        actual.Transport.ShouldBe(expected.Transport);
        actual.AuthType.ShouldBe(expected.AuthType);
        actual.AuthHeaderName.ShouldBe(expected.AuthHeaderName);
        actual.BaseEndpoint.ShouldBe(expected.BaseEndpoint);
        actual.Models.ShouldBe(expected.Models);
        actual.Capabilities.ShouldBe(expected.Capabilities);
        actual.AllowRemotePlainHttp.ShouldBe(expected.AllowRemotePlainHttp);
    }
}
