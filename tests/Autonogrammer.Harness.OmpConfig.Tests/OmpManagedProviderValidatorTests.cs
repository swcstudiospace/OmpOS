using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

public sealed class OmpManagedProviderValidatorTests
{
    [Fact]
    public void TheFixtureProvidersAreValid()
    {
        OmpManagedProviderValidator.Validate(OmpModelsFixtures.Local()).ShouldBeNull();
        OmpManagedProviderValidator.Validate(OmpModelsFixtures.Keyed()).ShouldBeNull();
    }

    [Theory]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("https://api.example.com/v1/")]
    [InlineData("https://host.example:8443")]
    [InlineData("HTTP://HOST/v1")]
    public void AcceptsHttpAndHttpsBaseUrls(string endpoint)
    {
        OmpManagedProviderValidator.Validate(OmpModelsFixtures.Local() with { BaseUrl = new Uri(endpoint) }).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("host/v1")]
    [InlineData("ftp://host/v1")]
    [InlineData("file:///etc/passwd")]
    [InlineData("http://user@host/v1")]
    [InlineData("http://user:pw@host/v1")]
    [InlineData("http://host/v1?key=1")]
    [InlineData("http://host/v1?")]
    [InlineData("http://host/v1#frag")]
    [InlineData(" http://host/v1")]
    [InlineData("http://host/v1\n")]
    [InlineData("http://host/\"quote")]
    public void RejectsOtherBaseUrls(string endpoint)
    {
        OmpManagedProviderValidator.Validate(OmpModelsFixtures.Local() with { BaseUrl = new Uri(endpoint, UriKind.RelativeOrAbsolute) }).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Acme")]
    [InlineData("-acme")]
    [InlineData("a b")]
    [InlineData("acme\n")]
    [InlineData("a:b")]
    public void RejectsInvalidProviderIds(string id)
    {
        OmpManagedProviderValidator.Validate(OmpModelsFixtures.Local(id)).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("MY_PROVIDER_API_KEY")]
    [InlineData("K")]
    [InlineData("A1_B2")]
    public void AcceptsEnvironmentVariableNames(string name)
    {
        var provider = OmpModelsFixtures.Keyed() with { ApiKeyEnvName = name };

        OmpManagedProviderValidator.Validate(provider).ShouldBeNull();
    }

    [Theory]
    [InlineData("sk-abcdef0123456789")]
    [InlineData("api_key")]
    [InlineData("1KEY")]
    [InlineData("_KEY")]
    [InlineData("KEY-X")]
    [InlineData("KEY NAME")]
    [InlineData("!cat /etc/secret")]
    [InlineData("$(whoami)")]
    [InlineData("KEY\n")]
    [InlineData("")]
    public void RefusesLiteralKeysAndCommandForms(string value)
    {
        var provider = OmpModelsFixtures.Keyed() with { ApiKeyEnvName = value };

        var problem = OmpManagedProviderValidator.Validate(provider);

        problem.ShouldNotBeNull();

        if (value.Length > 8)
        {
            problem.ShouldNotContain(value, Case.Sensitive);
        }
    }

    [Fact]
    public void RequiresExactlyOneWayToAuthenticate()
    {
        var both = OmpModelsFixtures.Keyed() with { NoAuthentication = true };
        var neither = OmpModelsFixtures.Keyed() with { ApiKeyEnvName = null };

        OmpManagedProviderValidator.Validate(both).ShouldNotBeNull();
        OmpManagedProviderValidator.Validate(neither).ShouldNotBeNull();
    }

    [Fact]
    public void RejectsAnUndefinedProtocol()
    {
        OmpManagedProviderValidator.Validate(OmpModelsFixtures.Local() with { Api = (OmpProviderApi)99 }).ShouldNotBeNull();
    }

    [Fact]
    public void RequiresAtLeastOneModel()
    {
        OmpManagedProviderValidator.Validate(OmpModelsFixtures.Local() with { Models = [] }).ShouldNotBeNull();
    }

    [Fact]
    public void RejectsDuplicateModelIds()
    {
        var provider = OmpModelsFixtures.Local() with { Models = [new OmpManagedModel("m"), new OmpManagedModel("m", "Other")] };

        OmpManagedProviderValidator.Validate(provider).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("tab\tid")]
    [InlineData("new\nline")]
    [InlineData("bell\u0007")]
    public void RejectsModelIdsWithWhitespaceOrControlCharacters(string modelId)
    {
        var provider = OmpModelsFixtures.Local() with { Models = [new OmpManagedModel(modelId)] };

        OmpManagedProviderValidator.Validate(provider).ShouldNotBeNull();
    }

    [Fact]
    public void AcceptsAModelIdOf200CharactersAndRejects201()
    {
        var ok = OmpModelsFixtures.Local() with { Models = [new OmpManagedModel(new string('m', OmpManagedModel.MaxIdLength))] };
        var tooLong = OmpModelsFixtures.Local() with { Models = [new OmpManagedModel(new string('m', OmpManagedModel.MaxIdLength + 1))] };

        OmpManagedProviderValidator.Validate(ok).ShouldBeNull();
        OmpManagedProviderValidator.Validate(tooLong).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("two\nlines")]
    [InlineData("bell\u0007")]
    [InlineData("line\u2028separator")]
    public void RejectsModelNamesThatAreEmptyOrNotOnOneLine(string name)
    {
        var provider = OmpModelsFixtures.Local() with { Models = [new OmpManagedModel("m", name)] };

        OmpManagedProviderValidator.Validate(provider).ShouldNotBeNull();
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-5, null)]
    [InlineData(null, 0)]
    [InlineData(null, -1)]
    public void RejectsNonPositiveLimits(int? contextWindow, int? maxTokens)
    {
        var provider = OmpModelsFixtures.Local() with { Models = [new OmpManagedModel("m", ContextWindow: contextWindow, MaxTokens: maxTokens)] };

        OmpManagedProviderValidator.Validate(provider).ShouldNotBeNull();
    }
}
