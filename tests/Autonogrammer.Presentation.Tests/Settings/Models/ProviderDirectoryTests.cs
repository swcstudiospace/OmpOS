using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>The provider listing: sections by region and kind, region and kind filters with counts, text search, and the way a reload keeps the cards.</summary>
public sealed class ProviderDirectoryTests
{
    private static async Task<(ModelsSettingsPage Page, ListingModelsSource Source, ProviderDirectoryModel Directory)> OpenAsync(params ProviderListing[] listings)
    {
        var source = new ListingModelsSource(listings.Length == 0 ? ListingTestData.All() : listings);
        var page = new ModelsSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (page, source, page.Directory);
    }

    private static string[] Headings(ProviderDirectoryModel directory) => [.. directory.Sections.Select(section => section.Heading)];

    private static string[] Ids(ProviderDirectoryModel directory) => [.. directory.Sections.SelectMany(section => section.Cards).Select(card => card.Id)];

    [Fact]
    public async Task ListingsAreGroupedByRegionThenKindInAFixedOrder()
    {
        var (page, _, directory) = await OpenAsync().ConfigureAwait(true);

        directory.HasListings.ShouldBeTrue();
        directory.Cards.Count.ShouldBe(9);
        Headings(directory).ShouldBe(
        [
            "Europe: Model APIs",
            "United States and Canada: Model APIs",
            "United States and Canada: Coding plans",
            "China: Model APIs",
            "China: Coding plans",
            "Local runtimes",
        ]);
        Ids(directory).ShouldBe(["mistral", "infomaniak-ai-tools", "perplexity", "anthropic-subscription", "baichuan", "deepseek", "zai", "ollama", "localai"]);
        page.State.ShouldBe(ModelsPageState.Ready);
    }

    [Fact]
    public async Task APageWithListingsButNoProvidersIsReadyNotEmpty()
    {
        var (page, _, directory) = await OpenAsync().ConfigureAwait(true);

        page.Groups.ShouldBeEmpty();
        page.State.ShouldBe(ModelsPageState.Ready);
        directory.VisibleCount.ShouldBe(9);
        page.HasNoMatches.ShouldBeFalse();
    }

    [Fact]
    public async Task APageWithNeitherProvidersNorListingsIsEmpty()
    {
        var source = new ListingModelsSource();
        var page = new ModelsSettingsPage(source);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(ModelsPageState.Empty);
        page.Directory.HasListings.ShouldBeFalse();
    }

    [Fact]
    public async Task TheRegionChoicesListOnlyRegionsWithListingsAndCountThem()
    {
        var (_, _, directory) = await OpenAsync().ConfigureAwait(true);

        directory.RegionChoices.ShouldBe(["All (9)", "Europe (2)", "United States and Canada (2)", "China (3)", "Local runtimes (2)"]);
        directory.KindChoices.ShouldBe(["All (9)", "Model APIs (5)", "Aggregators and gateways (0)", "Coding plans (2)", "Local runtimes (2)"]);
    }

    [Fact]
    public async Task SelectingARegionByIndexKeepsOnlyItsListings()
    {
        var (_, _, directory) = await OpenAsync().ConfigureAwait(true);
        var names = PropertyChangeRecorder.Record(directory);

        directory.SelectedRegionIndex = 3;

        directory.SelectedRegion.ShouldBe(ProviderListingRegion.China);
        Ids(directory).ShouldBe(["baichuan", "deepseek", "zai"]);
        directory.Announcement.ShouldBe("3 listings shown.");
        names.ShouldContain(nameof(ProviderDirectoryModel.Sections), StringComparer.Ordinal);
        names.ShouldContain(nameof(ProviderDirectoryModel.SelectedRegionIndex), StringComparer.Ordinal);
        directory.KindChoices[0].ShouldBe("All (3)");
    }

    [Fact]
    public async Task SelectingAKindKeepsOnlyItsListingsAndCombinesWithTheRegion()
    {
        var (_, _, directory) = await OpenAsync().ConfigureAwait(true);

        directory.SelectKind(ProviderListingKind.CodingPlan);
        Ids(directory).ShouldBe(["anthropic-subscription", "zai"]);

        directory.SelectRegion(ProviderListingRegion.China);
        Ids(directory).ShouldBe(["zai"]);
        directory.RegionChoices.ShouldBe(["All (2)", "Europe (0)", "United States and Canada (1)", "China (1)", "Local runtimes (0)"]);

        directory.SelectedKindIndex = 0;
        directory.SelectedKind.ShouldBeNull();
        Ids(directory).ShouldBe(["baichuan", "deepseek", "zai"]);
    }

    [Fact]
    public async Task AListControlReportingMinusOneWhileItRebuildsIsNotAChoice()
    {
        var (_, _, directory) = await OpenAsync().ConfigureAwait(true);
        directory.SelectedRegionIndex = 1;
        directory.SelectedKindIndex = 1;

        directory.SelectedRegionIndex = -1;
        directory.SelectedKindIndex = -1;

        directory.SelectedRegion.ShouldBe(ProviderListingRegion.Europe);
        directory.SelectedKind.ShouldBe(ProviderListingKind.Api);
    }

    [Fact]
    public async Task AnIndexOutsideTheChoicesMeansAll()
    {
        var (_, _, directory) = await OpenAsync().ConfigureAwait(true);
        directory.SelectedRegionIndex = 2;

        directory.SelectedRegionIndex = 99;

        directory.SelectedRegion.ShouldBeNull();
        directory.VisibleCount.ShouldBe(9);
    }

    [Fact]
    public async Task ARegionWithoutListingsCannotBeSelected()
    {
        var (_, _, directory) = await OpenAsync(ListingTestData.Mistral()).ConfigureAwait(true);

        directory.SelectRegion(ProviderListingRegion.China);

        directory.SelectedRegion.ShouldBeNull();
        directory.RegionChoices.ShouldBe(["All (1)", "Europe (1)"]);
    }

    [Theory]
    [InlineData("mistral", new[] { "mistral" })]
    [InlineData("Switzerland", new[] { "infomaniak-ai-tools" })]
    [InlineData("api.deepseek.com", new[] { "deepseek" })]
    [InlineData("inferred, not stated", new[] { "mistral" })]
    [InlineData("coding plans", new[] { "anthropic-subscription", "zai" })]
    [InlineData("verify before use", new[] { "baichuan" })]
    [InlineData("anthropic-messages", new[] { "deepseek" })]
    [InlineData("  OLLAMA ", new[] { "ollama" })]
    public async Task SearchMatchesNameCountryResidencyEndpointKindAndNote(string text, string[] expected)
    {
        var (_, _, directory) = await OpenAsync().ConfigureAwait(true);

        directory.SearchText = text;

        Ids(directory).ShouldBe(expected);
    }

    [Fact]
    public async Task SearchCountsTheChoicesAndAnnouncesThroughThePage()
    {
        var (page, _, directory) = await OpenAsync().ConfigureAwait(true);

        page.SearchText = "deepseek";

        page.Directory.SearchText.ShouldBe("deepseek");
        Ids(directory).ShouldBe(["deepseek"]);
        directory.RegionChoices.ShouldBe(["All (1)", "Europe (0)", "United States and Canada (0)", "China (1)", "Local runtimes (0)"]);
        page.Announcement.ShouldBe("1 provider shown.");
        page.HasNoMatches.ShouldBeFalse();

        page.SearchText = "no such provider anywhere";

        directory.HasNoMatches.ShouldBeTrue();
        page.HasNoMatches.ShouldBeTrue();
        page.Announcement.ShouldBe("0 providers shown.");
    }

    [Fact]
    public async Task AReloadKeepsTheCardsAndWhatTheUserTyped()
    {
        var (page, source, directory) = await OpenAsync().ConfigureAwait(true);
        var card = directory.Find("infomaniak-ai-tools").ShouldNotBeNull();
        card.BeginSetup();
        card.UrlInputs.Single().Value = "424242";
        card.ModelIdsText = "llama3";

        source.Listings[source.Listings.FindIndex(listing => string.Equals(listing.Id, "infomaniak-ai-tools", StringComparison.Ordinal))] = ListingTestData.Infomaniak() with { DisplayName = "Infomaniak AI" };
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        directory.Find("infomaniak-ai-tools").ShouldBeSameAs(card);
        card.DisplayName.ShouldBe("Infomaniak AI");
        card.IsSetupOpen.ShouldBeTrue();
        card.UrlInputs.Single().Value.ShouldBe("424242");
        card.ModelIdsText.ShouldBe("llama3");
    }

    [Fact]
    public async Task AListingThatLeavesTheSourceDisappearsAndItsRegionFilterResets()
    {
        var (page, source, directory) = await OpenAsync().ConfigureAwait(true);
        directory.SelectRegion(ProviderListingRegion.Local);

        source.Listings.RemoveAll(listing => listing.Region == ProviderListingRegion.Local);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        directory.SelectedRegion.ShouldBeNull();
        directory.Find("ollama").ShouldBeNull();
        directory.VisibleCount.ShouldBe(7);
    }

    [Fact]
    public async Task AListingAlreadySetUpInOmpSaysSo()
    {
        var (page, source, directory) = await OpenAsync().ConfigureAwait(true);
        var card = directory.Find("localai").ShouldNotBeNull();
        card.ConfiguredText.ShouldBeNull();

        card.BeginSetup();
        (await card.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        card.ConfiguredText.ShouldBe("Set up in omp.");
        source.LoadCalls.ShouldBe(2);
        page.Directory.Find("mistral")!.ConfiguredText.ShouldBeNull();
    }

    [Fact]
    public async Task NullArgumentsThrow()
    {
        var (_, _, directory) = await OpenAsync().ConfigureAwait(true);

        Should.Throw<ArgumentNullException>(() => directory.Find(null!));
    }
}
