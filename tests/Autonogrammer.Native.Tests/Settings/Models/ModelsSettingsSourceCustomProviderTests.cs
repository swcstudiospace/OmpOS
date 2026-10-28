using System.Text.Json;
using Autonogrammer.Native.Settings.Models;
using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Models;

/// <summary>
/// Saving and removing custom providers through <see cref="ModelsSettingsSource"/>: the form becomes an Autonogrammer block in <c>models.yml</c>
/// (what omp reads) and an entry in the registry file (what omp's file cannot hold), every write status becomes a plain
/// <see cref="ModelsActionResult"/>, and a refused write leaves both files as they were.
/// </summary>
public sealed class ModelsSettingsSourceCustomProviderTests
{
    private const string ConflictText =
        "A provider with this id already exists in models.yml that Autonogrammer does not own. Choose another id, or remove that provider from models.yml by hand.";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AKeyedAnthropicProviderIsWrittenWithTheKeyVariableNameAndNoKey()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var result = await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        var yml = fake.ReadModelsYml().ShouldNotBeNull();
        yml.ShouldContain("# autonogrammer:begin gateway", Case.Sensitive);
        yml.ShouldContain("baseUrl: \"https://llm.example.com/anthropic\"", Case.Sensitive);
        yml.ShouldContain("api: anthropic-messages", Case.Sensitive);
        yml.ShouldContain("apiKey: \"AUTONOGRAMMER_PROVIDER_GATEWAY_KEY\"", Case.Sensitive);
        yml.ShouldContain("auth: apiKey", Case.Sensitive);
        yml.ShouldContain("- id: \"claude-sonnet\"", Case.Sensitive);
        yml.ShouldContain("- id: \"llama3:8b\"", Case.Sensitive);
        yml.ShouldNotContain("auth: none", Case.Sensitive);
        var details = await File.ReadAllTextAsync(fake.DetailsPath, Token).ConfigureAwait(true);
        details.ShouldContain("Acme Gateway", Case.Sensitive);
        details.ShouldContain("BearerToken", Case.Sensitive);
        details.ShouldContain("Embeddings", Case.Sensitive);
    }

    [Fact]
    public async Task AKeylessProviderIsWrittenAsAuthNone()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Local("acme"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var yml = fake.ReadModelsYml().ShouldNotBeNull();
        yml.ShouldContain("auth: none", Case.Sensitive);
        yml.ShouldContain("api: openai-completions", Case.Sensitive);
        yml.ShouldNotContain("apiKey", Case.Sensitive);
    }

    [Fact]
    public async Task AnEndpointThatSpeaksOnlyTheResponsesRouteIsWrittenAsOpenAiResponses()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        (await composition.Source.SaveCustomProviderAsync(ModelsForms.ResponsesOnly("resp"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var yml = fake.ReadModelsYml().ShouldNotBeNull();
        yml.ShouldContain("api: openai-responses", Case.Sensitive);
        yml.ShouldContain("apiKey: \"AUTONOGRAMMER_PROVIDER_RESP_KEY\"", Case.Sensitive);
        yml.ShouldNotContain("auth: apiKey", Case.Sensitive);
    }

    [Theory]
    [InlineData("id", "Bad Id", "ErrIdInvalid")]
    [InlineData("endpoint", "http://remote.example.com/v1", "ErrEndpointPlainHttpRemote")]
    [InlineData("duplicate", "claude-sonnet", "ErrModelDuplicate")]
    [InlineData("model", "has space", "ErrModelInvalid")]
    [InlineData("name", " ", "ErrDisplayNameRequired")]
    public async Task ABrokenRuleIsAPlainFailureAndNothingIsWritten(string what, string value, string expected)
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var form = ModelsForms.Gateway("gateway");
        form = what switch
        {
            "id" => form with { Id = value },
            "endpoint" => form with { BaseEndpoint = new Uri(value), Protocol = CustomProviderProtocol.OpenAiCompatible },
            "duplicate" => form with { Models = [value, value] },
            "model" => form with { Models = [value] },
            "name" => form with { DisplayName = value },
            _ => throw new ArgumentOutOfRangeException(nameof(what), what, "Unknown rule in the test data."),
        };

        var result = await composition.Source.SaveCustomProviderAsync(form, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldBe(CopyOf(expected));
        fake.ReadModelsYml().ShouldBeNull();
        fake.Calls().ShouldBeEmpty();
    }

    [Fact]
    public async Task AnIdThatOmpUsesForItsOwnProviderIsRefused()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var openai = await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("openai"), Token).ConfigureAwait(true);
        var ollama = await composition.Source.SaveCustomProviderAsync(ModelsForms.Local("ollama"), Token).ConfigureAwait(true);

        openai.Succeeded.ShouldBeFalse();
        openai.Message.ShouldNotBeNull().ShouldContain("omp already has a provider", Case.Sensitive);
        ollama.Succeeded.ShouldBeFalse();
        fake.ReadModelsYml().ShouldBeNull();
    }

    [Fact]
    public async Task AProviderThatExistsInModelsYmlOutsideAnAutonogrammerBlockIsAConflictAndNothingChanges()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        const string Existing = "# mine\nproviders:\n  acme:\n    baseUrl: x\n";
        fake.WriteModelsYml(Existing);
        using var composition = fake.Compose();

        var result = await composition.Source.SaveCustomProviderAsync(ModelsForms.Local("acme"), Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldBe(ConflictText);
        fake.ReadModelsYml().ShouldBe(Existing);
        (File.Exists(fake.DetailsPath) ? await File.ReadAllTextAsync(fake.DetailsPath, Token).ConfigureAwait(true) : string.Empty).ShouldNotContain("acme", Case.Sensitive);
    }

    [Fact]
    public async Task ALayoutTheEditorDoesNotTouchIsAPlainFailureAndNothingChanges()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        const string Existing = "providers: {}\n";
        fake.WriteModelsYml(Existing);
        using var composition = fake.Compose();

        var result = await composition.Source.SaveCustomProviderAsync(ModelsForms.Local("acme"), Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("flow style", Case.Sensitive);
        fake.ReadModelsYml().ShouldBe(Existing);
    }

    [Fact]
    public async Task AProviderOmpsOwnValidationRefusesIsRolledBackInBothFiles()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        const string Existing = "# mine\nproviders:\n";
        fake.WriteModelsYml(Existing);
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Local("good"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        var goodYml = fake.ReadModelsYml();
        var goodDetails = await File.ReadAllTextAsync(fake.DetailsPath, Token).ConfigureAwait(true);

        var bad = ModelsForms.Local("bad") with { Models = ["BADMODEL"] };
        var result = await composition.Source.SaveCustomProviderAsync(bad, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNullOrWhiteSpace();
        result.Message.ShouldNotContain(fake.Root, Case.Sensitive);
        fake.ReadModelsYml().ShouldBe(goodYml);
        (await File.ReadAllTextAsync(fake.DetailsPath, Token).ConfigureAwait(true)).ShouldBe(goodDetails);
    }

    /// <summary>
    /// Two valid ids never share a key variable (an id holds only lowercase letters, digits and hyphens, and only the hyphen becomes
    /// an underscore), so the collision is reached through a block that already names the variable: one an earlier Autonogrammer wrote
    /// under another name. The new provider is refused and nothing is written for it.
    /// </summary>
    [Fact]
    public async Task AnIdWhoseKeyVariableAnotherBlockAlreadyUsesIsRefused()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("legacy"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        var taken = ProviderKeyEnvironment.EnvNameFor("a-b");
        var written = fake.ReadModelsYml().ShouldNotBeNull();
        written.ShouldContain(ProviderKeyEnvironment.EnvNameFor("legacy"), Case.Sensitive);
        fake.WriteModelsYml(written.Replace(ProviderKeyEnvironment.EnvNameFor("legacy"), taken, StringComparison.Ordinal));
        var before = fake.ReadModelsYml();

        var second = await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("a-b"), Token).ConfigureAwait(true);

        second.Succeeded.ShouldBeFalse();
        second.Message.ShouldNotBeNull().ShouldContain("key variable", Case.Sensitive);
        fake.ReadModelsYml().ShouldBe(before);
    }

    [Fact]
    public async Task ASavedProviderCanBeEditedAndSavedAgainUnchanged()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var original = ModelsForms.Gateway("gateway");
        (await composition.Source.SaveCustomProviderAsync(original, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        var written = fake.ReadModelsYml();
        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);
        var definition = snapshot.Providers.Single(provider => string.Equals(provider.Id, "gateway", StringComparison.Ordinal)).Definition.ShouldNotBeNull();

        var again = await composition.Source.SaveCustomProviderAsync(definition, Token).ConfigureAwait(true);

        again.Succeeded.ShouldBeTrue();
        fake.ReadModelsYml().ShouldBe(written);
        definition.DisplayName.ShouldBe(original.DisplayName);
        definition.BaseEndpoint.ShouldBe(original.BaseEndpoint);
        definition.Capabilities.ShouldBe(original.Capabilities);
        definition.Auth.ShouldBe(original.Auth);
        definition.Models.ShouldBe(original.Models);
    }

    [Fact]
    public async Task ChangingAKeyedProviderToNoAuthenticationDeletesItsStoredKey()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await composition.Source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var result = await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway") with { Auth = CustomProviderAuth.None }, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        fake.ReadModelsYml().ShouldNotBeNull().ShouldContain("auth: none", Case.Sensitive);
        (await File.ReadAllTextAsync(fake.SecretsPath, Token).ConfigureAwait(true)).ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        composition.Keys.Current.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemovingAProviderDeletesItsBlockItsDetailsAndItsKeyAndNoOtherProvider()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("other"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await composition.Source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await composition.Source.SaveApiKeyAsync("other", "other-key-value", Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var result = await composition.Source.RemoveCustomProviderAsync("gateway", Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        var yml = fake.ReadModelsYml().ShouldNotBeNull();
        yml.ShouldNotContain("autonogrammer:begin gateway", Case.Sensitive);
        yml.ShouldContain("autonogrammer:begin other", Case.Sensitive);
        using var secrets = JsonDocument.Parse(await File.ReadAllBytesAsync(fake.SecretsPath, Token).ConfigureAwait(true));
        secrets.RootElement.GetProperty("secrets").EnumerateObject().Select(static property => property.Name).ShouldBe(["AUTONOGRAMMER_PROVIDER_OTHER_KEY"]);
        (await File.ReadAllTextAsync(fake.SecretsPath, Token).ConfigureAwait(true)).ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        var details = await File.ReadAllTextAsync(fake.DetailsPath, Token).ConfigureAwait(true);
        details.ShouldNotContain("\"gateway\"", Case.Sensitive);
        details.ShouldContain("\"other\"", Case.Sensitive);
        composition.Keys.Current.Keys.ShouldBe(["AUTONOGRAMMER_PROVIDER_OTHER_KEY"]);
        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);
        snapshot.Providers.Where(provider => provider.Group == ProviderGroup.Custom).Select(provider => provider.Id).ShouldBe(["other"]);
    }

    [Fact]
    public async Task OnlyAProviderAutonogrammerWroteCanBeRemoved()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        const string Existing = "providers:\n  mine:\n    baseUrl: x\n";
        fake.WriteModelsYml(Existing);
        using var composition = fake.Compose();

        var foreign = await composition.Source.RemoveCustomProviderAsync("mine", Token).ConfigureAwait(true);
        var missing = await composition.Source.RemoveCustomProviderAsync("nothing", Token).ConfigureAwait(true);
        var invalid = await composition.Source.RemoveCustomProviderAsync("Not Valid", Token).ConfigureAwait(true);

        foreign.Succeeded.ShouldBeFalse();
        foreign.Message.ShouldNotBeNull().ShouldContain("did not write", Case.Sensitive);
        missing.Succeeded.ShouldBeFalse();
        invalid.Succeeded.ShouldBeFalse();
        fake.ReadModelsYml().ShouldBe(Existing);
    }

    [Fact]
    public async Task AnUnreadableRegistryFileRefusesTheSaveAndLeavesModelsYmlAlone()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        await File.WriteAllTextAsync(fake.DetailsPath, "not json", Token).ConfigureAwait(true);
        using var composition = fake.Compose();

        var result = await composition.Source.SaveCustomProviderAsync(ModelsForms.Local("acme"), Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("Fix or remove that file", Case.Sensitive);
        fake.ReadModelsYml().ShouldBeNull();
        (await File.ReadAllTextAsync(fake.DetailsPath, Token).ConfigureAwait(true)).ShouldBe("not json");
    }

    [Fact]
    public async Task ConcurrentSavesAreSerialisedAndNoChangeIsLost()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var ids = Enumerable.Range(0, 6).Select(static index => "prov" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        var saves = ids.Select(id => composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway(id), Token));
        var results = await Task.WhenAll(saves).ConfigureAwait(true);
        var keys = ids.Select(id => composition.Source.SaveApiKeyAsync(id, "value-" + id, Token));
        var keyResults = await Task.WhenAll(keys).ConfigureAwait(true);

        results.ShouldAllBe(result => result.Succeeded);
        keyResults.ShouldAllBe(result => result.Succeeded);
        var yml = fake.ReadModelsYml().ShouldNotBeNull();
        foreach (var id in ids)
        {
            yml.ShouldContain("# autonogrammer:begin " + id, Case.Sensitive);
        }

        using var details = JsonDocument.Parse(await File.ReadAllBytesAsync(fake.DetailsPath, Token).ConfigureAwait(true));
        details.RootElement.GetProperty("providers").GetArrayLength().ShouldBe(ids.Length);
        using var secrets = JsonDocument.Parse(await File.ReadAllBytesAsync(fake.SecretsPath, Token).ConfigureAwait(true));
        secrets.RootElement.GetProperty("secrets").EnumerateObject().Count().ShouldBe(ids.Length);
    }

    private static string CopyOf(string name) => name switch
    {
        "ErrIdInvalid" => ModelsCopy.ErrIdInvalid,
        "ErrEndpointPlainHttpRemote" => ModelsCopy.ErrEndpointPlainHttpRemote,
        "ErrModelDuplicate" => ModelsCopy.ErrModelDuplicate,
        "ErrModelInvalid" => ModelsCopy.ErrModelInvalid,
        "ErrDisplayNameRequired" => ModelsCopy.ErrDisplayNameRequired,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown message name."),
    };

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }
    }
}
