using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Providers.Tests;

/// <summary>Filling the <c>{name}</c> placeholders of a base URL: only plain identifiers fit, nothing can add a path, a host or a credential, and the result must be https.</summary>
public sealed class CatalogueUrlTemplateTests
{
    private static Dictionary<string, string> Values(string name, string value) => new(StringComparer.Ordinal) { [name] = value };

    [Fact]
    public void VariablesAreListedInOrderWithoutDuplicates() =>
        CatalogueUrlTemplate.VariablesIn("https://{a}.{b}.test/{a}/v1").ShouldBe(["a", "b"]);

    [Fact]
    public void ATemplateWithoutPlaceholdersHasNoVariables() => CatalogueUrlTemplate.VariablesIn("https://api.vendor.test/v1").ShouldBeEmpty();

    [Fact]
    public void AFilledTemplateResolvesToAnAbsoluteHttpsUrl()
    {
        var result = CatalogueUrlTemplate.Resolve("https://api.infomaniak.com/2/ai/{product_id}/openai/v1", Values("product_id", " 123456 "), allowLoopbackHttp: false);

        result.Succeeded.ShouldBeTrue();
        result.Url.ShouldNotBeNull().AbsoluteUri.ShouldBe("https://api.infomaniak.com/2/ai/123456/openai/v1");
        result.Problem.ShouldBeNull();
    }

    [Fact]
    public void ASecondVariableIsFilledToo()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["account_id"] = "acc1", ["gateway_id"] = "gw-2" };

        CatalogueUrlTemplate.Resolve("https://gateway.ai.test/v1/{account_id}/{gateway_id}/compat", values, allowLoopbackHttp: false)
            .Url.ShouldNotBeNull().AbsoluteUri.ShouldBe("https://gateway.ai.test/v1/acc1/gw-2/compat");
    }

    [Fact]
    public void AMissingOrBlankValueNamesTheVariable()
    {
        CatalogueUrlTemplate.Resolve("https://{zone}.vendor.test/v1", new Dictionary<string, string>(StringComparer.Ordinal), allowLoopbackHttp: false)
            .Problem.ShouldBe("Enter a value for zone.");
        CatalogueUrlTemplate.Resolve("https://{zone}.vendor.test/v1", Values("zone", "  "), allowLoopbackHttp: false)
            .Problem.ShouldBe("Enter a value for zone.");
    }

    [Theory]
    [InlineData("evil.test/path")]
    [InlineData("a@b")]
    [InlineData("a?b=1")]
    [InlineData("a#b")]
    [InlineData("a b")]
    [InlineData("a:443")]
    [InlineData("{zone}")]
    [InlineData("-leading")]
    public void AValueThatCouldChangeTheAddressIsRefused(string value)
    {
        var result = CatalogueUrlTemplate.Resolve("https://{zone}.vendor.test/v1", Values("zone", value), allowLoopbackHttp: false);

        result.Succeeded.ShouldBeFalse();
        result.Problem.ShouldNotBeNull().ShouldContain("zone", Case.Sensitive);
    }

    [Theory]
    [InlineData("your-resource-name")]
    [InlineData("example")]
    [InlineData("my-example-host")]
    [InlineData("xxxx")]
    [InlineData("placeholder")]
    public void APlaceholderLookingValueIsRefused(string value) =>
        CatalogueUrlTemplate.IsAcceptableValue(value).ShouldBeFalse();

    [Theory]
    [InlineData("de-fra-1")]
    [InlineData("adb-123.4.azuredatabricks.net")]
    [InlineData("org-account")]
    [InlineData("eu-central-1")]
    public void AnIdentifierIsAccepted(string value) => CatalogueUrlTemplate.IsAcceptableValue(value).ShouldBeTrue();

    [Fact]
    public void AValueOver128CharactersIsRefused() =>
        CatalogueUrlTemplate.IsAcceptableValue(new string('a', CatalogueUrlTemplate.MaxValueLength + 1)).ShouldBeFalse();

    [Fact]
    public void PlainHttpIsRefusedForARemoteHost()
    {
        var result = CatalogueUrlTemplate.Resolve("http://api.vendor.test/v1", new Dictionary<string, string>(StringComparer.Ordinal), allowLoopbackHttp: true);

        result.Succeeded.ShouldBeFalse();
        result.Problem.ShouldNotBeNull().ShouldContain("https", Case.Sensitive);
    }

    [Fact]
    public void PlainHttpToLoopbackNeedsTheLocalFlag()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        CatalogueUrlTemplate.Resolve("http://localhost:11434/v1", values, allowLoopbackHttp: true).Url.ShouldNotBeNull();
        CatalogueUrlTemplate.Resolve("http://localhost:11434/v1", values, allowLoopbackHttp: false).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void AnUnresolvedBraceInTheTemplateIsRefused() =>
        CatalogueUrlTemplate.Resolve("https://api.vendor.test/{Bad}/v1", new Dictionary<string, string>(StringComparer.Ordinal), allowLoopbackHttp: false)
            .Problem.ShouldNotBeNull().ShouldContain("unresolved", Case.Sensitive);

    [Fact]
    public void NullArgumentsThrow()
    {
        Should.Throw<ArgumentNullException>(() => CatalogueUrlTemplate.VariablesIn(null!));
        Should.Throw<ArgumentNullException>(() => CatalogueUrlTemplate.Resolve(null!, new Dictionary<string, string>(StringComparer.Ordinal), false));
        Should.Throw<ArgumentNullException>(() => CatalogueUrlTemplate.Resolve("https://x.test", null!, false));
    }
}
