using Autonogrammer.Harness.OmpConfig;
using Autonogrammer.Native.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Models;

/// <summary>The table of API-key providers omp knows by itself: only verified rows, each with a usable id, a key page that carries its read date, and no row for a provider that failed the check.</summary>
public sealed class OmpBuiltInProvidersTests
{
    [Fact]
    public void EveryIdIsUniqueAndAProviderIdOmpAccepts()
    {
        OmpBuiltInProviders.All.Select(static provider => provider.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(OmpBuiltInProviders.All.Count);
        OmpBuiltInProviders.All.ShouldAllBe(static provider => OmpProviderId.IsValid(provider.Id));
        OmpBuiltInProviders.All.ShouldAllBe(static provider => provider.DisplayName.Length > 0);
    }

    [Fact]
    public void AKeyPageAlwaysComesWithItsReadDateAndIsAnAbsoluteHttpsAddress()
    {
        foreach (var provider in OmpBuiltInProviders.All)
        {
            if (provider.KeyPage is null)
            {
                provider.ReadOn.ShouldBeNull(provider.Id);
            }
            else
            {
                provider.KeyPage.IsAbsoluteUri.ShouldBeTrue(provider.Id);
                provider.KeyPage.Scheme.ShouldBe(Uri.UriSchemeHttps, provider.Id);
                provider.ReadOn.ShouldBe(new DateOnly(2026, 10, 26), provider.Id);
            }
        }
    }

    [Theory]
    [InlineData("siliconflow")]
    [InlineData("perplexity")]
    [InlineData("cohere")]
    public void AProviderThatListedNoModelInTheSandboxIsNotInTheTable(string providerId)
    {
        OmpBuiltInProviders.Find(providerId).ShouldBeNull();
    }

    [Fact]
    public void LookupsFindRowsByExactId()
    {
        OmpBuiltInProviders.Find("openai").ShouldNotBeNull().DisplayName.ShouldBe("OpenAI");
        OmpBuiltInProviders.Find("OPENAI").ShouldBeNull();
        OmpBuiltInProviders.FindLocalEngine("lm-studio").ShouldNotBeNull().Endpoint.ShouldBe(new Uri("http://127.0.0.1:1234/v1"));
        OmpBuiltInProviders.FindLocalEngine("openai").ShouldBeNull();
        OmpBuiltInProviders.LocalEngines.Select(static engine => engine.Id).ShouldBe(["ollama", "lm-studio", "llama.cpp"]);
        OmpBuiltInProviders.LocalEngines.ShouldAllBe(static engine => OmpProviderId.IsValid(engine.Id) && engine.Endpoint.IsLoopback);
    }
}
