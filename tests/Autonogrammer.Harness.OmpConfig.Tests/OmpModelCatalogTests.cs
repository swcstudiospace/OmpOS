using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

public sealed class OmpModelCatalogTests
{
    // Two rows copied from omp 18.12.6 output, plus members a newer omp could add.
    private const string RealShape =
        """
        {"models":[
          {"provider":"autonogrammer-test","kind":"chat","id":"autonogrammer-model-small","selector":"autonogrammer-test/autonogrammer-model-small","name":"Autonogrammer Small","contextWindow":8192,"maxTokens":2048,"reasoning":false,"thinking":null,"input":["text"],"cost":{"input":0,"output":0,"cacheRead":0,"cacheWrite":0},"pricingStatus":"unknown"},
          {"provider":"openai","kind":"chat","id":"codex-mini-latest","selector":"openai/codex-mini-latest","name":"Codex Mini","contextWindow":200000,"maxTokens":100000,"reasoning":true,"thinking":["minimal","low","medium","high","xhigh"],"input":["text","image"],"cost":{"input":1.5,"output":6,"cacheRead":0.375,"cacheWrite":0},"pricingStatus":"fixed","someFutureMember":{"nested":[1,2,3]}}
        ],"futureTopLevel":true}
        """;

    [Fact]
    public async Task ParsesTheRealShapeAndIgnoresUnknownMembers()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson(RealShape);
        var catalog = new OmpModelCatalog(fake.Options());

        var listing = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        listing.IsComplete.ShouldBeTrue();
        listing.Warning.ShouldBeNull();
        listing.Models.Count.ShouldBe(2);

        var small = listing.Models[0];
        small.ProviderId.ShouldBe("autonogrammer-test");
        small.ModelId.ShouldBe("autonogrammer-model-small");
        small.Selector.ShouldBe("autonogrammer-test/autonogrammer-model-small");
        small.DisplayName.ShouldBe("Autonogrammer Small");
        small.Kind.ShouldBe("chat");
        small.ContextWindow.ShouldBe(8192);
        small.MaxTokens.ShouldBe(2048);
        small.Reasoning.ShouldBeFalse();
        small.ThinkingLevels.ShouldBeEmpty();
        small.InputKinds.ShouldBe(["text"]);
        small.AcceptsImages.ShouldBeFalse();
        small.PricingStatus.ShouldBe("unknown");

        var codex = listing.Models[1];
        codex.Selector.ShouldBe("openai/codex-mini-latest");
        codex.Reasoning.ShouldBeTrue();
        codex.ThinkingLevels.ShouldBe(["minimal", "low", "medium", "high", "xhigh"]);
        codex.AcceptsImages.ShouldBeTrue();
        codex.ContextWindow.ShouldBe(200000);
        codex.Cost.ShouldBe(new OmpModelCost(1.5, 6, 0.375, 0));
    }

    [Fact]
    public async Task RunsOnlyTheOfflineListingWithoutExtensions()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson(RealShape);
        var catalog = new OmpModelCatalog(fake.Options());

        _ = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        fake.Calls().Single().Arguments.ShouldBe(["models", "--json", "--no-extensions"]);
    }

    [Fact]
    public async Task FillsDefaultsForSparseRowsAndSkipsRowsWithoutAnId()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson("""{"models":[{"provider":"acme","id":"bare"},{"provider":"acme"},{"id":"orphan"}]}""");
        var catalog = new OmpModelCatalog(fake.Options());

        var listing = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var model = listing.Models.Single();
        model.Selector.ShouldBe("acme/bare");
        model.DisplayName.ShouldBe("bare");
        model.Kind.ShouldBe("chat");
        model.ContextWindow.ShouldBeNull();
        model.Cost.ShouldBeNull();
        model.InputKinds.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnEmptyListingIsCompleteBecauseOmpGatesTheListByCredentials()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson("""{"models":[]}""");
        var catalog = new OmpModelCatalog(fake.Options());

        var listing = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        listing.Models.ShouldBeEmpty();
        listing.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task AValidationWarningMakesTheListingIncompleteAndKeepsItsFirstLine()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson("""{"models":[{"provider":"openai","id":"gpt-4"}]}""");
        fake.SetModelsStandardError(FakeOmpEnvironment.ValidationWarningLine + "\n" + FakeOmpEnvironment.ValidationErrorLine + "\n");
        var catalog = new OmpModelCatalog(fake.Options());

        var listing = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        listing.IsComplete.ShouldBeFalse();
        listing.Warning.ShouldBe(FakeOmpEnvironment.ValidationWarningLine);
        listing.Models.Single().Selector.ShouldBe("openai/gpt-4");
    }

    [Fact]
    public async Task TheWarningNeverShowsTheAgentDirectoryOrHome()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson("""{"models":[]}""");
        fake.SetModelsStandardError($"Warning: models.yml validation failed at {fake.AgentDirectory}/models.yml for {fake.Home}\n");
        var catalog = new OmpModelCatalog(fake.Options());

        var listing = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        listing.Warning.ShouldNotBeNull();
        listing.Warning.ShouldNotContain(fake.Root, Case.Sensitive);
        listing.Warning.ShouldContain("<agent dir>/models.yml", Case.Sensitive);
    }

    [Fact]
    public async Task OtherStandardErrorTextIsAWarningButTheListingStaysComplete()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson("""{"models":[]}""");
        fake.SetModelsStandardError("model cache refreshed\n");
        var catalog = new OmpModelCatalog(fake.Options());

        var listing = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        listing.IsComplete.ShouldBeTrue();
        listing.Warning.ShouldBe("model cache refreshed");
    }

    [Fact]
    public async Task ANonZeroExitIsAnIncompleteEmptyListingWithOmpsFirstErrorLine()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsExit(1);
        fake.SetModelsStandardError("fatal: catalog unreadable\nsecond line\n");
        var catalog = new OmpModelCatalog(fake.Options());

        var listing = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        listing.Models.ShouldBeEmpty();
        listing.IsComplete.ShouldBeFalse();
        listing.Warning.ShouldBe("fatal: catalog unreadable");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("{}")]
    [InlineData("")]
    public async Task UnreadableOutputIsAnIncompleteEmptyListing(string output)
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson(output);
        var catalog = new OmpModelCatalog(fake.Options());

        var listing = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        listing.Models.ShouldBeEmpty();
        listing.IsComplete.ShouldBeFalse();
        listing.Warning.ShouldNotBeNull();
    }

    [Fact]
    public async Task AMissingOmpIsAnIncompleteEmptyListingNotAnException()
    {
        using var fake = new FakeOmpEnvironment();
        var catalog = new OmpModelCatalog(fake.OptionsWithoutOmp());

        var listing = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        listing.Models.ShouldBeEmpty();
        listing.IsComplete.ShouldBeFalse();
        listing.Warning.ShouldNotBeNull();
    }

    [Fact]
    public async Task TruncatedOutputIsNeverParsedAsACompleteListing()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson(RealShape);
        var catalog = new OmpModelCatalog(fake.Options(maxOutputBytes: 200));

        var listing = await catalog.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        listing.IsComplete.ShouldBeFalse();
        listing.Models.ShouldBeEmpty();
    }
}
