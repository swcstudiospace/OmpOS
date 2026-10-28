using OmpOS.Harness.Providers;
using OmpOS.Harness.Secrets;
using OmpOS.Native.Settings.Models;
using OmpOS.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Models;

/// <summary>
/// Adding a catalogue listing through <see cref="ModelsSettingsSource"/> with a fake omp: a row omp knows gets its key through omp,
/// every other route becomes an OmpOS block whose <c>apiKey</c> is the NAME of a variable and whose key sits in the key store, and each
/// refusal is a typed outcome that leaves <c>models.yml</c> and the key store as they were. No outcome, file or call carries the key.
/// </summary>
public sealed class ModelsSettingsSourceCatalogueTests
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>(StringComparer.Ordinal);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ASnapshotListsEveryCatalogueRowInOrderWithNoKey()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);

        var listings = snapshot.Listings.ShouldNotBeNull();
        listings.Select(static listing => listing.Id).ShouldBe(CatalogueRows.All.Select(static row => row.Id));
        listings.ShouldAllBe(static listing => listing.SourceUrl.IsAbsoluteUri);
        listings.Single(static listing => string.Equals(listing.Id, "moonshot-cn", StringComparison.Ordinal)).Routes[0].TakesKey.ShouldBeTrue();
    }

    [Fact]
    public async Task ABlockRowIsWrittenWithTheKeyVariableNameAndTheKeyIsOnlyInTheKeyStore()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var outcome = await Source(composition).AddCatalogueAsync(Request("moonshot-cn", "openai"), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.Added);
        outcome.Message.ShouldBeNull();
        var yml = fake.ReadModelsYml().ShouldNotBeNull();
        yml.ShouldContain("# ompos:begin moonshot-cn", Case.Sensitive);
        yml.ShouldContain("baseUrl: \"https://api.moonshot.cn/v1\"", Case.Sensitive);
        yml.ShouldContain("apiKey: \"OMPOS_PROVIDER_MOONSHOT_CN_KEY\"", Case.Sensitive);
        (await File.ReadAllTextAsync(fake.SecretsPath, Token).ConfigureAwait(true)).ShouldContain(ModelsForms.Canary, Case.Sensitive);
        fake.ReadEverything(fake.SecretsPath).Values.ShouldNotContain(text => text.Contains(ModelsForms.Canary, StringComparison.Ordinal));
        fake.Calls().SelectMany(static call => call.Arguments).ShouldNotContain(argument => argument.Contains(ModelsForms.Canary, StringComparison.Ordinal));

        var reloaded = await composition.Source.LoadAsync(Token).ConfigureAwait(true);
        reloaded.Providers.Single(static provider => string.Equals(provider.Id, "moonshot-cn", StringComparison.Ordinal)).Credential.ShouldBe(CredentialState.Present);
    }

    [Fact]
    public async Task AddingTheSameRowAgainUpdatesItAndKeepsTheStoredKeyWhenNoneIsGiven()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var source = Source(composition);
        (await source.AddCatalogueAsync(Request("moonshot-cn", "openai"), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true)).Status.ShouldBe(CatalogueAddStatus.Added);

        var again = await source.AddCatalogueAsync(Request("moonshot-cn", "openai"), string.Empty, planRuleAccepted: false, Token).ConfigureAwait(true);

        again.Status.ShouldBe(CatalogueAddStatus.Updated);
        (await File.ReadAllTextAsync(fake.SecretsPath, Token).ConfigureAwait(true)).ShouldContain(ModelsForms.Canary, Case.Sensitive);
    }

    [Fact]
    public async Task ARouteThatTakesAKeyWithNoKeyAndNoStoredKeyIsKeyMissingAndWritesNothing()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var outcome = await Source(composition).AddCatalogueAsync(Request("moonshot-cn", "openai"), "  ", planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.KeyMissing);
        outcome.Message.ShouldNotBeNullOrWhiteSpace();
        fake.ReadModelsYml().ShouldBeNull();
        File.Exists(fake.SecretsPath).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("your-workspace")]
    [InlineData("a/b")]
    [InlineData("ws.evil.com/x")]
    public async Task APlaceholderThatIsMissingOrNotAnIdentifierIsInvalidUrlVariable(string value)
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var request = Request("dashscope-intl", CatalogueRows.Find("dashscope-intl")!.Routes[0].Id, ("workspace_id", value));

        var outcome = await Source(composition).AddCatalogueAsync(request, ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.InvalidUrlVariable);
        fake.ReadModelsYml().ShouldBeNull();
        File.Exists(fake.SecretsPath).ShouldBeFalse();
    }

    [Fact]
    public async Task APlaceholderValueIsPutIntoTheBaseUrl()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var route = CatalogueRows.Find("dashscope-intl")!.Routes[0];
        var request = Request("dashscope-intl", route.Id, ("workspace_id", "ws-4711"), models: ["qwen-plus"]);

        var outcome = await Source(composition).AddCatalogueAsync(request, ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.Added);
        fake.ReadModelsYml().ShouldNotBeNull().ShouldContain("https://ws-4711.", Case.Sensitive);
    }

    [Fact]
    public async Task ARouteWithNoDocumentedModelAndNoDiscoveryNeedsAModelId()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var source = Source(composition);

        var without = await source.AddCatalogueAsync(Request("zai-api", "openai"), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);
        var with = await source.AddCatalogueAsync(Request("zai-api", "openai", models: ["glm-4.6"]), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        without.Status.ShouldBe(CatalogueAddStatus.InvalidRequest);
        with.Status.ShouldBe(CatalogueAddStatus.Added);
        fake.ReadModelsYml().ShouldNotBeNull().ShouldContain("- id: \"glm-4.6\"", Case.Sensitive);
    }

    [Fact]
    public async Task ACodingPlanNeedsTheRuleTextToBeReadFirst()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var source = Source(composition);

        var refused = await source.AddCatalogueAsync(Request("ark-coding-plan", "openai"), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);
        fake.ReadModelsYml().ShouldBeNull();
        var accepted = await source.AddCatalogueAsync(Request("ark-coding-plan", "openai"), ModelsForms.Canary, planRuleAccepted: true, Token).ConfigureAwait(true);

        refused.Status.ShouldBe(CatalogueAddStatus.PlanRuleNotAccepted);
        accepted.Status.ShouldBe(CatalogueAddStatus.Added);
    }

    [Fact]
    public async Task ThePresentationRequestCarriesTheRuleAcceptanceToTheSource()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var plain = Request("ark-coding-plan", "openai");

        var refused = await composition.Source.AddCatalogueProviderAsync(plain, ModelsForms.Canary, Token).ConfigureAwait(true);
        var accepted = await composition.Source.AddCatalogueProviderAsync(plain with { PlanRuleAccepted = true }, ModelsForms.Canary, Token).ConfigureAwait(true);

        refused.Succeeded.ShouldBeFalse();
        refused.Message.ShouldNotBeNull().ShouldContain("rule text", Case.Sensitive);
        accepted.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ASignInRowHasNoKeyAndNoBlockAndSaysHowToSignIn()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var row = CatalogueRows.All.First(static candidate => candidate.Routes.Count == 0);

        var outcome = await Source(composition).AddCatalogueAsync(Request(row.Id, "none"), ModelsForms.Canary, planRuleAccepted: true, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.Unavailable);
        outcome.Message.ShouldNotBeNullOrWhiteSpace();
        fake.ReadModelsYml().ShouldBeNull();
        fake.Calls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("not-a-row", "openai")]
    [InlineData("moonshot-cn", "no-such-route")]
    public async Task AnUnknownListingOrRouteIsInvalidRequest(string listing, string route)
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var outcome = await Source(composition).AddCatalogueAsync(Request(listing, route), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.InvalidRequest);
        fake.ReadModelsYml().ShouldBeNull();
    }

    [Fact]
    public async Task AKeyWithWhitespaceIsInvalidRequestAndNeverEchoed()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var outcome = await Source(composition).AddCatalogueAsync(Request("moonshot-cn", "openai"), "bad key " + ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.InvalidRequest);
        outcome.Message.ShouldNotBeNull().ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        fake.Calls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ARowOmpKnowsOnItsDefaultRouteGoesToOmpAndWritesNoBlock()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var source = Source(composition);

        var first = await source.AddCatalogueAsync(Request("deepseek", "openai"), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);
        var second = await source.AddCatalogueAsync(Request("deepseek", "openai"), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        first.Status.ShouldBe(CatalogueAddStatus.Added);
        second.Status.ShouldBe(CatalogueAddStatus.Updated);
        fake.ReadModelsYml().ShouldBeNull();
        File.Exists(fake.SecretsPath).ShouldBeFalse("OmpOS keeps no copy of a key omp stores itself");
        var import = fake.Calls().First(static call => string.Equals(call.Arguments[0], "auth", StringComparison.Ordinal));
        import.StandardInput.ShouldNotBeNull().ShouldContain("\"provider\":\"deepseek\"", Case.Sensitive);
    }

    [Fact]
    public async Task ARowOmpKnowsOnAnotherRouteGetsABlockUnderTheRouteId()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var outcome = await Source(composition).AddCatalogueAsync(Request("deepseek", "anthropic", models: ["deepseek-chat"]), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.Added);
        var yml = fake.ReadModelsYml().ShouldNotBeNull();
        yml.ShouldContain("# ompos:begin deepseek-anthropic", Case.Sensitive);
        yml.ShouldContain("api: anthropic-messages", Case.Sensitive);
        yml.ShouldContain("apiKey: \"OMPOS_PROVIDER_DEEPSEEK_ANTHROPIC_KEY\"", Case.Sensitive);
    }

    [Fact]
    public async Task ABlockOmpRefusesIsOmpRejectedAndTheFileAndKeyStoreAreAsTheyWere()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var outcome = await Source(composition).AddCatalogueAsync(Request("zai-api", "openai", models: ["BADMODEL"]), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.OmpRejected);
        outcome.Message.ShouldNotBeNullOrWhiteSpace();
        outcome.Message.ShouldNotBeNull().ShouldNotContain(fake.Root, Case.Sensitive);
        (fake.ReadModelsYml() ?? string.Empty).ShouldNotContain("zai-api", Case.Sensitive);
        File.Exists(fake.SecretsPath).ShouldBeFalse();
    }

    [Fact]
    public async Task AProviderOutsideAnOmpOsBlockWithTheSameIdIsOmpRejectedAndLeftAlone()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        const string Existing = "# mine\nproviders:\n  moonshot-cn:\n    baseUrl: x\n";
        fake.WriteModelsYml(Existing);
        using var composition = fake.Compose();

        var outcome = await Source(composition).AddCatalogueAsync(Request("moonshot-cn", "openai"), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.OmpRejected);
        fake.ReadModelsYml().ShouldBe(Existing);
        File.Exists(fake.SecretsPath).ShouldBeFalse();
    }

    [Fact]
    public async Task AKeyStoreThatRefusesTheKeyIsUnavailableAndTheNewBlockIsTakenBack()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose(secrets: new RefusingSecretStore());

        var outcome = await Source(composition).AddCatalogueAsync(Request("moonshot-cn", "openai"), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(CatalogueAddStatus.Unavailable);
        outcome.Message.ShouldNotBeNull().ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        (fake.ReadModelsYml() ?? string.Empty).ShouldNotContain("moonshot-cn", Case.Sensitive);
    }

    [Fact]
    public async Task AKeylessLocalRowIsWrittenAsAuthNoneOnLoopbackHttp()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var outcome = await Source(composition).AddCatalogueAsync(Request("llama-cpp", "openai", models: ["local-model"]), string.Empty, planRuleAccepted: false, Token).ConfigureAwait(true);

        outcome.IsSuccess.ShouldBeTrue();
        var yml = fake.ReadModelsYml().ShouldNotBeNull();
        yml.ShouldContain("auth: none", Case.Sensitive);
        yml.ShouldNotContain("apiKey", Case.Sensitive);
    }

    [Fact]
    public async Task ARemovedCatalogueBlockTakesItsKeyAndNothingElseWithIt()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        var source = Source(composition);
        (await source.AddCatalogueAsync(Request("moonshot-cn", "openai"), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true)).IsSuccess.ShouldBeTrue();
        (await source.AddCatalogueAsync(Request("zai-api", "openai", models: ["glm-4.6"]), ModelsForms.Canary, planRuleAccepted: false, Token).ConfigureAwait(true)).IsSuccess.ShouldBeTrue();

        var removed = await composition.Source.RemoveCustomProviderAsync("moonshot-cn", Token).ConfigureAwait(true);

        removed.Succeeded.ShouldBeTrue();
        var yml = fake.ReadModelsYml().ShouldNotBeNull();
        yml.ShouldNotContain("moonshot-cn", Case.Sensitive);
        yml.ShouldContain("# ompos:begin zai-api", Case.Sensitive);
        var keys = await File.ReadAllTextAsync(fake.SecretsPath, Token).ConfigureAwait(true);
        keys.ShouldNotContain("OMPOS_PROVIDER_MOONSHOT_CN_KEY", Case.Sensitive);
        keys.ShouldContain("OMPOS_PROVIDER_ZAI_API_KEY", Case.Sensitive);
    }

    [Fact]
    public void EveryRowOfTheCatalogueMapsToAListingWithTheSameRoutes()
    {
        var listings = CatalogueListingMapper.FromCatalogue();

        listings.Count.ShouldBe(CatalogueRows.All.Count);
        foreach (var (row, listing) in CatalogueRows.All.Zip(listings))
        {
            listing.Id.ShouldBe(row.Id);
            listing.Routes.Select(static route => route.Id).ShouldBe(row.Routes.Select(static route => route.Id));
            listing.Routes.Select(static route => route.TakesKey).ShouldBe(row.Routes.Select(static route => route.Auth != CatalogueAuthStyle.None));
            listing.Plan?.Tiers.Count.ShouldBe(row.Plan!.Tiers.Count);
            (listing.Plan is null).ShouldBe(row.Plan is null);
        }
    }

    private static ModelsSettingsSource Source(ModelsSettingsComposition composition) => (ModelsSettingsSource)composition.Source;

    private static CatalogueProviderRequest Request(string listing, string route, (string Name, string Value)? variable = null, IReadOnlyList<string>? models = null)
    {
        var values = variable is { } pair
            ? new Dictionary<string, string>(StringComparer.Ordinal) { [pair.Name] = pair.Value }
            : NoValues;
        return new CatalogueProviderRequest(listing, route, values, models ?? []);
    }

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }
    }

    /// <summary>A key store that fails every write the way a full disk does, with a message that has no value in it.</summary>
    private sealed class RefusingSecretStore : ISecretStore
    {
        public Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken) =>
            Task.FromException(new SecretStoreException("The secret file could not be written. The previous contents are unchanged."));

        public Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken) => Task.FromResult<SecretString?>(null);

        public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
