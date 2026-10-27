using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Providers.Tests;

/// <summary>
/// Checks the data-driven catalogue as a whole: it loads, every row passes the invariants (unique id, https source, status with a date,
/// https routes or loopback http for local runtimes, no placeholder host, declared URL variables, plan terms on every coding plan), no
/// retired or excluded vendor is offered, and every omp provider id a row names exists in omp's provider list.
/// </summary>
public sealed partial class CatalogueRowsTests
{
    private static readonly string[] ExcludedIds =
    [
        "github-models", "lambda", "lambda-inference", "replicate", "cursor", "watsonx", "yi-01ai", "deepl", "bfl-flux",
    ];

    [Fact]
    public void RowsLoadAndPassEveryInvariant()
    {
        CatalogueRows.All.Count.ShouldBeGreaterThan(90);
        var problems = CatalogueRowValidator.Validate(CatalogueRows.All);
        problems.ShouldBeEmpty(string.Join(" | ", problems));
    }

    [Fact]
    public void EveryRegionAndKindHasRows()
    {
        foreach (var region in Enum.GetValues<CatalogueRegion>())
        {
            CatalogueRows.All.Any(row => row.Region == region).ShouldBeTrue(region.ToString());
        }

        foreach (var kind in Enum.GetValues<CatalogueKind>())
        {
            CatalogueRows.All.Any(row => row.Kind == kind).ShouldBeTrue(kind.ToString());
        }
    }

    [Fact]
    public void IdsAreUniqueAndFindable()
    {
        CatalogueRows.All.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(CatalogueRows.All.Count);
        foreach (var row in CatalogueRows.All)
        {
            CatalogueRows.Find(row.Id).ShouldBeSameAs(row);
        }

        CatalogueRows.Find("no-such-provider").ShouldBeNull();
    }

    [Fact]
    public void RemoteRoutesAreHttpsAndOnlyLocalRuntimesUseLoopbackHttp()
    {
        foreach (var row in CatalogueRows.All)
        {
            foreach (var route in row.Routes)
            {
                var filled = Placeholder().Replace(route.BaseUrl, "value1");
                var url = new Uri(filled, UriKind.Absolute);
                if (row.Region == CatalogueRegion.Local)
                {
                    url.IsLoopback.ShouldBeTrue($"{row.Id}/{route.Id}");
                    route.Auth.ShouldBe(CatalogueAuthStyle.None, $"{row.Id}/{route.Id}");
                }
                else
                {
                    url.Scheme.ShouldBe(Uri.UriSchemeHttps, $"{row.Id}/{route.Id}");
                }
            }
        }
    }

    [Fact]
    public void EveryRowCarriesAnHttpsSourceAndADatedStatus()
    {
        foreach (var row in CatalogueRows.All)
        {
            row.SourceUrl.Scheme.ShouldBe(Uri.UriSchemeHttps, row.Id);
            row.StatusDate.ShouldBeGreaterThanOrEqualTo(new DateOnly(2026, 10, 22), row.Id);
            row.StatusDate.ShouldBeLessThanOrEqualTo(new DateOnly(2026, 10, 27), row.Id);
        }
    }

    [Fact]
    public void EveryCodingPlanCarriesTermsAndDefaultsCloudAgentsToPayAsYouGo()
    {
        var plans = CatalogueRows.All.Where(row => row.Kind == CatalogueKind.CodingPlan).ToList();
        plans.Count.ShouldBeGreaterThan(20);
        foreach (var row in plans)
        {
            var plan = row.Plan.ShouldNotBeNull(row.Id);
            plan.RuleText.Trim().Length.ShouldBeGreaterThan(20, row.Id);
            plan.CloudDefaultPayAsYouGo.ShouldBeTrue(row.Id);
            plan.Tiers.ShouldNotBeEmpty(row.Id);
            foreach (var tier in plan.Tiers.Where(tier => !tier.PriceVerified))
            {
                tier.PriceText.ShouldBe(CataloguePlanTier.UnverifiedPrice, $"{row.Id}/{tier.Name}");
            }
        }

        CatalogueRows.All.Where(row => row.Kind != CatalogueKind.CodingPlan).ShouldAllBe(row => row.Plan == null);
    }

    [Fact]
    public void UnverifiedRowsAskTheUserToVerifyBeforeUse()
    {
        var unverified = CatalogueRows.All.Where(row => row.Status == CatalogueStatus.Unverified).ToList();
        unverified.ShouldNotBeEmpty();
        foreach (var row in unverified)
        {
            row.VerifyBeforeUse.ShouldBeTrue(row.Id);
            row.Note.ShouldNotBeNull(row.Id).ShouldStartWith("Verify before use:", Case.Sensitive);
        }

        CatalogueRows.All.Where(row => row.Status != CatalogueStatus.Unverified).ShouldAllBe(row => !row.VerifyBeforeUse);
    }

    [Fact]
    public void RetiredAndExcludedVendorsAreNotOffered()
    {
        foreach (var id in ExcludedIds)
        {
            CatalogueRows.Find(id).ShouldBeNull(id);
        }

        var hosts = CatalogueRows.All.SelectMany(row => row.Routes).Select(route => route.BaseUrl).ToList();
        hosts.ShouldNotContain(url => url.Contains("api.hyperbolic.xyz", StringComparison.Ordinal));
        hosts.ShouldNotContain(url => url.Contains("api.lambda.ai", StringComparison.Ordinal));
        hosts.ShouldNotContain(url => url.Contains("api.replicate.com", StringComparison.Ordinal));
        hosts.ShouldNotContain(url => url.Contains("models.inference.ai.azure.com", StringComparison.Ordinal));
    }

    [Fact]
    public void HyperbolicUsesTheNewInferenceBase()
    {
        var row = CatalogueRows.Find("hyperbolic").ShouldNotBeNull();
        row.Routes.Select(route => route.BaseUrl).ShouldBe(["https://api.hyperbolic.ai/inference/v1"]);
    }

    [Fact]
    public void EveryOmpProviderIdNamedByARowExistsInOmp()
    {
        var known = OmpProviderIdFixture.Read();
        known.Count.ShouldBeGreaterThan(80);
        var named = CatalogueRows.All.Where(row => row.OmpProviderId is not null).ToList();
        named.Count.ShouldBeGreaterThan(30);
        foreach (var row in named)
        {
            known.ShouldContain(row.OmpProviderId!, row.Id);
            CatalogueRows.FindByOmpProviderId(row.OmpProviderId!).ShouldBeSameAs(row);
        }

        CatalogueRows.FindByOmpProviderId("no-such-omp-provider").ShouldBeNull();
    }

    [Fact]
    public void MistralIsThreeRowsForThreeKeyKinds()
    {
        var payg = CatalogueRows.Find("mistral").ShouldNotBeNull();
        var codestral = CatalogueRows.Find("mistral-codestral").ShouldNotBeNull();
        var vibe = CatalogueRows.Find("mistral-vibe-plan").ShouldNotBeNull();

        payg.Kind.ShouldBe(CatalogueKind.Api);
        payg.OmpProviderId.ShouldBe("mistral");
        codestral.Kind.ShouldBe(CatalogueKind.Api);
        codestral.OmpProviderId.ShouldBeNull();
        new Uri(codestral.Routes[0].BaseUrl).Host.ShouldBe("codestral.mistral.ai");
        vibe.Kind.ShouldBe(CatalogueKind.CodingPlan);
        vibe.Plan.ShouldNotBeNull().SeparateKey.ShouldBeTrue();
        vibe.OmpProviderId.ShouldBeNull();
        new[] { payg.Id, codestral.Id, vibe.Id }.Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
    }

    [Fact]
    public void AnthropicLoginIsASignInRowOmpOSNeverStores()
    {
        var subscription = CatalogueRows.Find("anthropic-subscription").ShouldNotBeNull();
        subscription.Routes.ShouldBeEmpty();
        subscription.LoginCommand.ShouldBe("/login anthropic");
        subscription.ApiKeyEnv.ShouldBeNull();
        subscription.TakesApiKey.ShouldBeFalse();
        subscription.Kind.ShouldBe(CatalogueKind.CodingPlan);

        var api = CatalogueRows.Find("anthropic").ShouldNotBeNull();
        api.TakesApiKey.ShouldBeTrue();
        api.Routes[0].Api.ShouldBe("anthropic-messages");
        api.Routes[0].Auth.ShouldBe(CatalogueAuthStyle.XApiKey);
        api.Note.ShouldNotBeNull().ShouldContain("never stores", Case.Sensitive);
    }

    [Fact]
    public void PlanRowsReachThePlanHostNotThePayAsYouGoHost()
    {
        (string Plan, string PayAsYouGo, bool SeparateKey)[] pairs =
        [
            ("zai", "zai-api", true),
            ("zhipu-coding-plan", "bigmodel", true),
            ("alibaba-coding-plan", "dashscope-cn", true),
            ("ark-coding-plan", "ark", false),
            ("qianfan-coding", "qianfan", true),
            ("iflytek-astron-coding", "iflytek-maas", true),
            ("stepfun-step-plan", "stepfun", false),
        ];
        foreach (var (planId, paygId, separateKey) in pairs)
        {
            var plan = CatalogueRows.Find(planId).ShouldNotBeNull(planId);
            var payg = CatalogueRows.Find(paygId).ShouldNotBeNull(paygId);
            plan.Plan.ShouldNotBeNull().SeparateKey.ShouldBe(separateKey, planId);
            var paygUrls = payg.Routes.Select(route => route.BaseUrl.TrimEnd('/')).ToHashSet(StringComparer.Ordinal);
            foreach (var route in plan.Routes)
            {
                paygUrls.ShouldNotContain(route.BaseUrl.TrimEnd('/'), $"{planId}/{route.Id}");
            }
        }
    }

    [Fact]
    public void AccountSpecificHostsDeclareTheirVariables()
    {
        VariableNames("infomaniak-ai-tools").ShouldBe(["product_id"]);
        VariableNames("exoscale-concrete-ai").ShouldContain("zone");
        VariableNames("dashscope-cn").ShouldBe(["workspace_id"]);
        VariableNames("azure").ShouldBe(["resource"]);
        VariableNames("amazon-bedrock").ShouldBe(["region"]);
    }

    [Fact]
    public void EveryRowNamesAnEnvironmentVariableNeverAKey()
    {
        var secretShape = SecretShape();
        foreach (var row in CatalogueRows.All)
        {
            if (row.ApiKeyEnv is not null)
            {
                row.ApiKeyEnv.ShouldMatch("^[A-Z][A-Z0-9_]*$", row.Id);
            }

            foreach (var text in new[] { row.Note, row.ResidencyNote, row.Plan?.RuleText }.Where(text => text is not null))
            {
                secretShape.IsMatch(text!).ShouldBeFalse($"{row.Id} holds something shaped like a key");
            }
        }
    }

    [Fact]
    public void ABlockProviderIdIsTheRowIdForTheDefaultRouteAndSuffixedForOthers()
    {
        var row = CatalogueRows.Find("deepseek").ShouldNotBeNull();
        row.BlockProviderId(row.Routes[0]).ShouldBe("deepseek");
        row.BlockProviderId(row.Routes[1]).ShouldBe("deepseek-anthropic");
        Should.Throw<ArgumentNullException>(() => row.BlockProviderId(null!));
    }

    [Fact]
    public void ABlockIdEqualsAnOmpProviderIdOnlyWhereTheRowIsThatProviderOrIsNamedHere()
    {
        // A models.yml block under an omp provider id overrides that provider (a Workers AI block, for example, lists as a judge model),
        // so a row may share an id with omp only when it is that provider, or for these routes that reach omp's own host.
        string[] reachedOnPurpose = ["minimax-cn", "xiaomi-token-plan-sgp", "xiaomi-token-plan-ams"];
        var known = OmpProviderIdFixture.Read();
        foreach (var row in CatalogueRows.All)
        {
            foreach (var route in row.Routes)
            {
                var blockId = row.BlockProviderId(route);
                if (known.Contains(blockId) && !string.Equals(row.OmpProviderId, blockId, StringComparison.Ordinal))
                {
                    reachedOnPurpose.ShouldContain(blockId, $"{row.Id}/{route.Id} writes a block that overrides the omp provider {blockId}");
                }
            }
        }
    }

    private static string[] VariableNames(string id) =>
        [.. CatalogueRows.Find(id).ShouldNotBeNull(id).UrlVariables.Select(variable => variable.Name)];

    [GeneratedRegex(@"\{[a-z][a-z0-9_]*\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\b(?:sk|fw|nvapi|hf|ghp|xai)[-_][A-Za-z0-9]{16,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SecretShape();
}
