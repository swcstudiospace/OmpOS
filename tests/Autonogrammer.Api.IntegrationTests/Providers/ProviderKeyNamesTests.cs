using Autonogrammer.Api.Services.Providers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Providers;

/// <summary>The variable name of a block key, and the path scrubber of omp messages.</summary>
public sealed class ProviderKeyNamesTests
{
    [Theory]
    [InlineData("gateway", "AUTONOGRAMMER_PROVIDER_GATEWAY_KEY")]
    [InlineData("my-llm.v2_x", "AUTONOGRAMMER_PROVIDER_MY_LLM_V2_X_KEY")]
    [InlineData("moonshot-cn", "AUTONOGRAMMER_PROVIDER_MOONSHOT_CN_KEY")]
    public void TheNameIsPrefixedUpperCasedAndPunctuationBecomesUnderscore(string id, string expected)
    {
        ProviderKeyNames.EnvNameFor(id).ShouldBe(expected);
    }

    [Fact]
    public void AnEmptyIdIsRefused()
    {
        Should.Throw<ArgumentException>(() => ProviderKeyNames.EnvNameFor(string.Empty));
    }

    [Theory]
    [InlineData("could not write /root/.omp/agent/models.yml now", "could not write <path> now")]
    [InlineData("see https://example.com/docs/page", "see https://example.com/docs/page")]
    [InlineData("provider a/b is fine", "provider a/b is fine")]
    [InlineData("backup at /tmp/x-1/models.yml.bak.", "backup at <path>.")]
    public void AnAbsolutePathIsReplacedAndAUrlOrAModelSelectorIsNot(string text, string expected)
    {
        PathScrubber.WithoutPaths(text).ShouldBe(expected);
    }
}
