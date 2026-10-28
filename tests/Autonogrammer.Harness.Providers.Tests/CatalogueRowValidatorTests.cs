using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Providers.Tests;

/// <summary>Each invariant of <see cref="CatalogueRowValidator"/> is broken on purpose in a valid sample row and must be reported by name.</summary>
public sealed class CatalogueRowValidatorTests
{
    private static readonly DateOnly Day = new(2026, 10, 27);

    private static CatalogueRoute Route(string baseUrl = "https://api.example-vendor.test/v1", CatalogueAuthStyle auth = CatalogueAuthStyle.Bearer, string? header = null) =>
        new("openai", "openai-completions", baseUrl, auth, header, Discovery: true, ModelsEndpoint: "GET /v1/models", Models: []);

    private static CatalogueRow Row(
        string id = "sample",
        CatalogueRoute? route = null,
        IReadOnlyList<CatalogueUrlVariable>? variables = null,
        CatalogueKind kind = CatalogueKind.Api,
        CatalogueRegion region = CatalogueRegion.Eu,
        CatalogueStatus status = CatalogueStatus.Verified,
        string? note = null,
        string? envName = "SAMPLE_API_KEY",
        string? country = "FR",
        CataloguePlanTerms? plan = null) =>
        new(
            id,
            "Sample",
            region,
            country,
            ResidencyNote: null,
            kind,
            OmpProviderId: null,
            envName,
            [route ?? Route()],
            variables ?? [],
            DocsUrl: null,
            KeyPageUrl: null,
            new Uri("https://docs.example-vendor.test/api"),
            status,
            Day,
            note,
            LoginCommand: null,
            plan);

    private static CataloguePlanTerms Plan(string rule = "Interactive use in coding tools only; no automated scripts.", bool cloudDefault = true, CataloguePlanTier? tier = null) =>
        new(
            rule,
            RuleSourceUrl: null,
            InteractiveOnly: true,
            cloudDefault,
            SeparateKey: true,
            new Uri("https://plan.example-vendor.test/"),
            ValidityDays: null,
            [tier ?? new CataloguePlanTier("Lite", "$10 per month", PriceVerified: true)],
            []);

    private static IReadOnlyList<string> Check(params CatalogueRow[] rows) => CatalogueRowValidator.Validate(rows);

    [Fact]
    public void AValidSamplePasses() => Check(Row()).ShouldBeEmpty();

    [Fact]
    public void ADuplicateIdIsReported() => Check(Row(), Row()).ShouldContain(problem => problem.Contains("used by two rows", StringComparison.Ordinal));

    [Fact]
    public void AnIdThatIsNotASlugIsReported() => Check(Row(id: "Not A Slug")).ShouldContain(problem => problem.Contains("not a slug", StringComparison.Ordinal));

    [Fact]
    public void PlainHttpToARemoteHostIsReported() =>
        Check(Row(route: Route("http://api.vendor.test/v1"))).ShouldContain(problem => problem.Contains("plain http", StringComparison.Ordinal));

    [Fact]
    public void PlainHttpToLoopbackIsAllowedForALocalRuntimeOnly()
    {
        var local = Row(
            route: Route("http://127.0.0.1:8080/v1", CatalogueAuthStyle.None),
            kind: CatalogueKind.Local,
            region: CatalogueRegion.Local,
            envName: null,
            country: null);
        Check(local).ShouldBeEmpty();
        Check(Row(route: Route("http://127.0.0.1:8080/v1"))).ShouldContain(problem => problem.Contains("plain http", StringComparison.Ordinal));
    }

    [Fact]
    public void APlaceholderHostIsReported() =>
        Check(Row(route: Route("https://your-host.example.com/v1"))).ShouldContain(problem => problem.Contains("placeholder host", StringComparison.Ordinal));

    [Fact]
    public void AKeyOrQueryInTheBaseUrlIsReported()
    {
        Check(Row(route: Route("https://api.vendor.test/v1?key=abc"))).ShouldContain(problem => problem.Contains("query", StringComparison.Ordinal));
        Check(Row(route: Route("https://user:pw@api.vendor.test/v1"))).ShouldContain(problem => problem.Contains("user information", StringComparison.Ordinal));
    }

    [Fact]
    public void UndeclaredAndUnusedUrlVariablesAreReported()
    {
        Check(Row(route: Route("https://{zone}.vendor.test/v1"))).ShouldContain(problem => problem.Contains("URL variables", StringComparison.Ordinal));
        Check(Row(variables: [new CatalogueUrlVariable("zone", "Zone")])).ShouldContain(problem => problem.Contains("URL variables", StringComparison.Ordinal));
        Check(Row(route: Route("https://{zone}.vendor.test/v1"), variables: [new CatalogueUrlVariable("zone", "Zone")])).ShouldBeEmpty();
    }

    [Fact]
    public void AnUnknownApiValueIsReported() =>
        Check(Row(route: Route() with { Api = "openai-chat" })).ShouldContain(problem => problem.Contains("not an omp api value", StringComparison.Ordinal));

    [Fact]
    public void ACustomHeaderNeedsItsNameAndNothingElseMayHaveOne()
    {
        Check(Row(route: Route(auth: CatalogueAuthStyle.CustomHeader))).ShouldContain(problem => problem.Contains("auth header", StringComparison.Ordinal));
        Check(Row(route: Route(auth: CatalogueAuthStyle.CustomHeader, header: "X-API-Key"))).ShouldBeEmpty();
        Check(Row(route: Route(header: "X-API-Key"))).ShouldContain(problem => problem.Contains("auth header", StringComparison.Ordinal));
    }

    [Fact]
    public void ACodingPlanNeedsTermsWithARuleAndACloudDefault()
    {
        Check(Row(kind: CatalogueKind.CodingPlan)).ShouldContain(problem => problem.Contains("needs plan terms", StringComparison.Ordinal));
        Check(Row(kind: CatalogueKind.CodingPlan, plan: Plan(rule: " "))).ShouldContain(problem => problem.Contains("no rule text", StringComparison.Ordinal));
        Check(Row(kind: CatalogueKind.CodingPlan, plan: Plan(cloudDefault: false))).ShouldContain(problem => problem.Contains("pay-as-you-go", StringComparison.Ordinal));
        Check(Row(kind: CatalogueKind.CodingPlan, plan: Plan())).ShouldBeEmpty();
        Check(Row(plan: Plan())).ShouldContain(problem => problem.Contains("only a coding plan", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnverifiedTierPriceMustSayPriceUnverified() =>
        Check(Row(kind: CatalogueKind.CodingPlan, plan: Plan(tier: new CataloguePlanTier("Lite", "$10 per month", PriceVerified: false))))
            .ShouldContain(problem => problem.Contains("price unverified", StringComparison.Ordinal));

    [Fact]
    public void AnUnverifiedRowNeedsTheVerifyNote()
    {
        Check(Row(status: CatalogueStatus.Unverified)).ShouldContain(problem => problem.Contains("Verify before use:", StringComparison.Ordinal));
        Check(Row(status: CatalogueStatus.Unverified, note: "Verify before use: no vendor page was readable.")).ShouldBeEmpty();
    }

    [Fact]
    public void ALongNoteIsReported() =>
        Check(Row(note: new string('x', CatalogueRowValidator.MaxNoteLength + 1))).ShouldContain(problem => problem.Contains("longer than", StringComparison.Ordinal));

    [Fact]
    public void AKeyThatIsNotAnEnvironmentNameIsReported() =>
        Check(Row(envName: "my key")).ShouldContain(problem => problem.Contains("environment variable NAME", StringComparison.Ordinal));

    [Fact]
    public void ABadCountryAndAMismatchedLocalKindAreReported()
    {
        Check(Row(country: "France")).ShouldContain(problem => problem.Contains("ISO 3166-1", StringComparison.Ordinal));
        Check(Row(kind: CatalogueKind.Local)).ShouldContain(problem => problem.Contains("local kind", StringComparison.Ordinal));
    }

    [Fact]
    public void ARowWithoutARouteOrASignInCommandIsReported() =>
        Check(Row() with { Routes = [] }).ShouldContain(problem => problem.Contains("route or an omp sign-in", StringComparison.Ordinal));

    [Fact]
    public void NullRowsThrow() => Should.Throw<ArgumentNullException>(() => CatalogueRowValidator.Validate(null!));
}
