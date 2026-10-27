using Shouldly;
using Xunit;

namespace OmpOS.Harness.Providers.Tests;

/// <summary>The explicit id map keeps every typed <see cref="ProviderCatalogue"/> id working: it names a row and a route of the data catalogue that reach the same endpoint, and the omp id of that row.</summary>
public sealed class ProviderIdMapTests
{
    [Fact]
    public void EveryLegacyCatalogueIdIsMappedInCatalogueOrder() =>
        ProviderIdMap.All.Select(mapping => mapping.LegacyId).ShouldBe(ProviderCatalogue.All.Select(entry => entry.Id));

    [Fact]
    public void EveryMappingNamesARowAndARouteThatExist()
    {
        foreach (var mapping in ProviderIdMap.All)
        {
            var row = CatalogueRows.Find(mapping.RowId).ShouldNotBeNull(mapping.LegacyId);
            row.Routes.Any(route => string.Equals(route.Id, mapping.RouteId, StringComparison.Ordinal)).ShouldBeTrue($"{mapping.LegacyId}: route {mapping.RouteId}");
            row.OmpProviderId.ShouldBe(mapping.OmpProviderId, mapping.LegacyId);
        }
    }

    [Fact]
    public void EveryMappedRouteReachesTheEndpointOfTheLegacyEntry()
    {
        foreach (var mapping in ProviderIdMap.All)
        {
            var entry = ProviderCatalogue.Find(mapping.LegacyId).ShouldNotBeNull();
            var endpoint = entry.BaseEndpoint.ShouldNotBeNull(mapping.LegacyId);
            var route = CatalogueRows.Find(mapping.RowId)!.Routes.Single(candidate => string.Equals(candidate.Id, mapping.RouteId, StringComparison.Ordinal));
            var expected = entry.Transport == ProviderTransport.AnthropicCompatible ? "anthropic-messages" : "openai";

            route.Api.ShouldStartWith(expected, Case.Sensitive, mapping.LegacyId);
            new Uri(route.BaseUrl).Scheme.ShouldBe(endpoint.Scheme, mapping.LegacyId);
            if (!string.Equals(mapping.LegacyId, "llama-cpp-local", StringComparison.Ordinal))
            {
                route.BaseUrl.TrimEnd('/').ShouldBe(endpoint.OriginalString.TrimEnd('/'), mapping.LegacyId);
            }
        }
    }

    [Fact]
    public void LlamaCppKeepsTheDocumentedPortConflictInTheRow()
    {
        // The earlier research recorded port 9931, omp's default is 8080; the row says so instead of hiding the conflict.
        ProviderCatalogue.LlamaCppLocalOpenAi.BaseEndpoint!.Port.ShouldBe(9931);
        var row = CatalogueRows.Find("llama-cpp").ShouldNotBeNull();
        new Uri(row.Routes[0].BaseUrl).Port.ShouldBe(8080);
        row.Note.ShouldNotBeNull().ShouldContain("9931", Case.Sensitive);
    }

    [Fact]
    public void FindReturnsNullForAnUnknownId()
    {
        ProviderIdMap.Find("ollama-local").ShouldNotBeNull().RowId.ShouldBe("ollama");
        ProviderIdMap.Find("no-such-id").ShouldBeNull();
        Should.Throw<ArgumentNullException>(() => ProviderIdMap.Find(null!));
    }

    [Fact]
    public void LegacyCodingPlanEntriesMatchRows()
    {
        foreach (var plan in ProviderCatalogue.CodingPlans)
        {
            var row = CatalogueRows.Find(plan.Id).ShouldNotBeNull(plan.Id);
            row.Kind.ShouldBe(CatalogueKind.CodingPlan, plan.Id);
            row.Plan.ShouldNotBeNull().InteractiveOnly.ShouldBe(plan.InteractiveToolsOnly, plan.Id);
        }
    }
}
