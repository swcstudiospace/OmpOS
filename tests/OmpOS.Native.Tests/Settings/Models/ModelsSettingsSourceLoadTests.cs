using OmpOS.Harness.OmpLauncher;
using OmpOS.Native.Settings.Models;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Models;

/// <summary>
/// What <see cref="ModelsSettingsSource.LoadAsync"/> reads through a fake omp: the listing grouped by provider, credential presence
/// through <c>omp token</c> for the providers that are shown, the default model, the templates and the warnings. omp is a shell
/// script; no real omp, model or network is involved.
/// </summary>
public sealed class ModelsSettingsSourceLoadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListedProvidersArePresentWithoutAQuestionAndTheOthersAreAskedThroughToken()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.SetModels(
            ModelsFakeOmp.Row("openai", "gpt-x"),
            ModelsFakeOmp.Row("openai", "gpt-y"),
            ModelsFakeOmp.Row("anthropic", "claude-z"),
            ModelsFakeOmp.Row("ollama", "llama3"));
        fake.SetTokenExit("groq", 0);
        using var composition = fake.Compose();

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        var openai = snapshot.Providers.Single(provider => string.Equals(provider.Id, "openai", StringComparison.Ordinal));
        openai.Group.ShouldBe(ProviderGroup.OmpBuiltIn);
        openai.Credential.ShouldBe(CredentialState.Present);
        openai.AuthKind.ShouldBe(ProviderAuthKind.ApiKey);
        openai.Models.ShouldBe(["openai/gpt-x", "openai/gpt-y"]);
        openai.SourceUrl.ShouldBeNull("the key page of this provider could not be read, so none is claimed");
        openai.RetrievedOn.ShouldBeNull();
        openai.IsOmpOSManaged.ShouldBeFalse();
        var anthropic = snapshot.Providers.Single(provider => string.Equals(provider.Id, "anthropic", StringComparison.Ordinal));
        anthropic.Credential.ShouldBe(CredentialState.Present);
        anthropic.SourceUrl.ShouldBe(new Uri("https://platform.claude.com/settings/keys"));
        anthropic.RetrievedOn.ShouldBe(new DateOnly(2026, 10, 26));
        snapshot.Providers.Single(provider => string.Equals(provider.Id, "groq", StringComparison.Ordinal)).Credential.ShouldBe(CredentialState.Present);
        var mistral = snapshot.Providers.Single(provider => string.Equals(provider.Id, "mistral", StringComparison.Ordinal));
        mistral.Credential.ShouldBe(CredentialState.Absent);
        mistral.Models.ShouldBeEmpty();
        var ollama = snapshot.Providers.Single(provider => string.Equals(provider.Id, "ollama", StringComparison.Ordinal));
        ollama.Group.ShouldBe(ProviderGroup.Local);
        ollama.Credential.ShouldBe(CredentialState.NotRequired);
        ollama.AuthKind.ShouldBe(ProviderAuthKind.None);
        ollama.EndpointText.ShouldBe("http://127.0.0.1:11434");
        ollama.Models.ShouldBe(["ollama/llama3"]);
        snapshot.Providers.Count.ShouldBe(OmpBuiltInProviders.All.Count + 1);
        snapshot.IsComplete.ShouldBeTrue();
        snapshot.Warning.ShouldBeNull();
        snapshot.DefaultModel.ShouldBeNull();

        var asked = fake.Calls().Where(call => string.Equals(call.Arguments[0], "token", StringComparison.Ordinal)).Select(call => call.Arguments[1]).Order(StringComparer.Ordinal).ToArray();
        asked.ShouldNotContain("openai", StringComparer.Ordinal);
        asked.ShouldNotContain("anthropic", StringComparer.Ordinal);
        asked.ShouldNotContain("ollama", StringComparer.Ordinal);
        asked.ShouldContain("groq", StringComparer.Ordinal);
        asked.ShouldContain("mistral", StringComparer.Ordinal);
        asked.Length.ShouldBe(OmpBuiltInProviders.All.Count - 2);
        asked.Distinct(StringComparer.Ordinal).Count().ShouldBe(asked.Length, "no provider is asked twice");
    }

    [Fact]
    public async Task AProviderOmpListsThatIsNotInTheTableGetsACardNamedByItsId()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.SetModels(ModelsFakeOmp.Row("foo-gateway", "m1"));
        using var composition = fake.Compose();

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        var card = snapshot.Providers.Single(provider => string.Equals(provider.Id, "foo-gateway", StringComparison.Ordinal));
        card.DisplayName.ShouldBe("foo-gateway");
        card.Group.ShouldBe(ProviderGroup.OmpBuiltIn);
        card.AuthKind.ShouldBe(ProviderAuthKind.Unknown);
        card.Credential.ShouldBe(CredentialState.Present);
        card.Models.ShouldBe(["foo-gateway/m1"]);
    }

    [Fact]
    public async Task TheDefaultModelIsTheDefaultRoleOfModelRoles()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.SetModels(ModelsFakeOmp.Row("openai", "gpt-x"));
        fake.SetRoles("{\"default\":\"openai/gpt-x\",\"smol\":\"openai/gpt-y\"}");
        using var composition = fake.Compose();

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        snapshot.DefaultModel.ShouldBe("openai/gpt-x");
    }

    [Fact]
    public async Task AValidationWarningOfModelsYmlBecomesTheSnapshotWarningAndTheListIsIncomplete()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.SetModelsStandardError(ModelsFakeOmp.ValidationWarningLine + "\n" + ModelsFakeOmp.ValidationErrorLine + "\n");
        using var composition = fake.Compose();

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        snapshot.IsComplete.ShouldBeFalse();
        snapshot.Warning.ShouldNotBeNull().ShouldContain("validation failed", Case.Sensitive);
        snapshot.Providers.Count.ShouldBe(OmpBuiltInProviders.All.Count, "the table still lists the providers a key can be added to");
        snapshot.Warning.ShouldNotContain(fake.Root, Case.Sensitive);
    }

    [Fact]
    public async Task AnUnsupportedLayoutOfModelsYmlIsAWarningNotAFailure()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.WriteModelsYml("providers: {}\n");
        using var composition = fake.Compose();

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        snapshot.Warning.ShouldNotBeNull().ShouldContain("flow style", Case.Sensitive);
        snapshot.Providers.ShouldNotContain(provider => provider.Group == ProviderGroup.Custom);
    }

    [Fact]
    public async Task WithoutOmpTheLoadFailsWithPlainTextAndNoPath()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose(locator: new OmpLocatorOptions(null, [], []));

        var failure = await Should.ThrowAsync<SettingsSourceException>(composition.Source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldBe(ModelsCopy.LoadFailed);
        failure.Message.ShouldNotContain(fake.Root, Case.Sensitive);
    }

    [Fact]
    public async Task WithoutOmpACustomProviderStillLoadsWithAWarningThatHidesPaths()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using (var good = fake.Compose())
        {
            var saved = await good.Source.SaveCustomProviderAsync(ModelsForms.Local("acme"), Token).ConfigureAwait(true);
            saved.Succeeded.ShouldBeTrue();
        }

        using var composition = fake.Compose(locator: new OmpLocatorOptions(null, [], []));

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        snapshot.Providers.ShouldContain(provider => provider.Id == "acme");
        snapshot.IsComplete.ShouldBeFalse();
        snapshot.Warning.ShouldNotBeNull();
        snapshot.Warning.ShouldNotContain(fake.Root, Case.Sensitive, "a path must not reach the page");
    }

    [Fact]
    public async Task TemplatesComeFromTheCatalogueWithSourceAndDate()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        snapshot.Templates.ShouldNotBeEmpty();
        snapshot.Templates.ShouldAllBe(template => template.SourceUrl != null && template.RetrievedOn != null);
        snapshot.Templates.Select(template => template.Group).Distinct().ShouldBe([ProviderGroup.Local, ProviderGroup.Cloud, ProviderGroup.CodingPlan], ignoreOrder: true);
    }

    [Fact]
    public async Task ACustomProviderCardShowsItsSavedDefinitionAndTheKeyState()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var keyed = ModelsForms.Gateway("gateway");
        (await composition.Source.SaveCustomProviderAsync(keyed, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Local("acme"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var before = await composition.Source.LoadAsync(Token).ConfigureAwait(true);
        (await composition.Source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        var after = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        var card = before.Providers.Single(provider => string.Equals(provider.Id, "gateway", StringComparison.Ordinal));
        card.Group.ShouldBe(ProviderGroup.Custom);
        card.IsOmpOSManaged.ShouldBeTrue();
        card.AuthKind.ShouldBe(ProviderAuthKind.ApiKey);
        card.Credential.ShouldBe(CredentialState.Absent);
        card.EndpointText.ShouldBe("https://llm.example.com/anthropic");
        card.Models.ShouldBe(["gateway/claude-sonnet", "gateway/llama3:8b"]);
        card.Note.ShouldNotBeNull().ShouldContain("plain text", Case.Sensitive);
        var definition = card.Definition.ShouldNotBeNull();
        definition.DisplayName.ShouldBe(keyed.DisplayName);
        definition.Protocol.ShouldBe(CustomProviderProtocol.AnthropicCompatible);
        definition.Auth.ShouldBe(CustomProviderAuth.Bearer);
        definition.Capabilities.ShouldBe(CustomProviderCapabilities.Chat | CustomProviderCapabilities.Embeddings);
        definition.Models.ShouldBe(["claude-sonnet", "llama3:8b"]);
        definition.AllowRemotePlainHttp.ShouldBeFalse();
        after.Providers.Single(provider => string.Equals(provider.Id, "gateway", StringComparison.Ordinal)).Credential.ShouldBe(CredentialState.Present);
        var keyless = after.Providers.Single(provider => string.Equals(provider.Id, "acme", StringComparison.Ordinal));
        keyless.Credential.ShouldBe(CredentialState.NotRequired);
        keyless.AuthKind.ShouldBe(ProviderAuthKind.None);
        keyless.Note.ShouldBeNull();
        keyless.Definition.ShouldNotBeNull().Auth.ShouldBe(CustomProviderAuth.None);
    }

    [Fact]
    public async Task ACustomProviderWithoutRegistryDetailsStillGetsAnEditableDefinition()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        File.Delete(fake.DetailsPath);

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        var card = snapshot.Providers.Single(provider => string.Equals(provider.Id, "gateway", StringComparison.Ordinal));
        card.DisplayName.ShouldBe("gateway");
        card.Definition.ShouldNotBeNull().Models.ShouldBe(["claude-sonnet", "llama3:8b"]);
        card.Definition!.Capabilities.ShouldBe(CustomProviderCapabilities.Chat);
        snapshot.Warning.ShouldBeNull();
    }

    [Fact]
    public async Task ACorruptRegistryFileIsAWarningAndNamesAreTakenFromModelsYml()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        await File.WriteAllTextAsync(fake.DetailsPath, "not json", Token).ConfigureAwait(true);

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        snapshot.Warning.ShouldNotBeNull().ShouldContain("saved details", Case.Sensitive);
        snapshot.Providers.Single(provider => string.Equals(provider.Id, "gateway", StringComparison.Ordinal)).DisplayName.ShouldBe("gateway");
        (await File.ReadAllTextAsync(fake.DetailsPath, Token).ConfigureAwait(true)).ShouldBe("not json");
    }

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }
    }
}
