using Autonogrammer.Api.Cloud.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>The <c>Cloud:Environment:Railway:WorkerNamePrefix</c> option: its default and the start-up rule that keeps it usable by the Railway client.</summary>
public sealed class WorkerNamePrefixOptionTests
{
    private const string Key = "Cloud:Environment:Railway:WorkerNamePrefix";

    private static readonly CloudOptionsValidator Validator = new();

    /// <summary>The default keeps the service names the platform has always used.</summary>
    [Fact]
    public void Default_IsAutonogrammerAgentPrefix()
    {
        new CloudOptions().Environment.Railway.WorkerNamePrefix.ShouldBe("autonogrammer-agent-");
    }

    /// <summary>A prefix with a space is refused and the key is named without repeating the value.</summary>
    [Fact]
    public void WithSpace_IsRefused()
    {
        using var keys = new CloudTestKeys();

        var result = Validator.Validate(null, Railway(keys, "bad prefix-"));

        result.Failed.ShouldBeTrue();
        Names(result, Key).ShouldBeTrue();
        result.Failures!.Any(static failure => failure.Contains("bad prefix", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>A prefix longer than the Railway client's label budget is refused.</summary>
    /// <param name="length">The prefix length.</param>
    [Theory]
    [InlineData(41)]
    [InlineData(65)]
    public void TooLong_IsRefused(int length)
    {
        using var keys = new CloudTestKeys();

        var result = Validator.Validate(null, Railway(keys, new string('a', length)));

        result.Failed.ShouldBeTrue();
        Names(result, Key).ShouldBeTrue();
    }

    /// <summary>The longest accepted prefix is 40 characters.</summary>
    [Fact]
    public void FortyCharacters_IsAccepted()
    {
        using var keys = new CloudTestKeys();

        Validator.Validate(null, Railway(keys, new string('a', 40))).Succeeded.ShouldBeTrue();
    }

    /// <summary>A custom lowercase prefix such as the one an existing deployment uses is accepted.</summary>
    [Fact]
    public void CustomValue_IsAccepted()
    {
        using var keys = new CloudTestKeys();

        Validator.Validate(null, Railway(keys, "ompos-worker")).Succeeded.ShouldBeTrue();
    }

    private static CloudOptions Railway(CloudTestKeys keys, string prefix)
    {
        var options = new CloudOptions
        {
            Enabled = true,
            Environment = { Provider = "Railway" },
            Worker = { SigningKeyPem = keys.SigningKeyPem },
        };
        options.Environment.Railway.ApiToken = "t";
        options.Environment.Railway.ProjectId = "p";
        options.Environment.Railway.EnvironmentId = "e";
        options.Environment.Railway.Image = "ghcr.io/swcstudiospace/autonogrammer-agent-worker:1";
        options.Environment.Railway.WorkerPublicKeysPem = [keys.WorkerPublicKeyPem];
        options.Environment.Railway.WorkerNamePrefix = prefix;
        return options;
    }

    private static bool Names(Microsoft.Extensions.Options.ValidateOptionsResult result, string key) =>
        result.Failures is { } failures && failures.Any(failure => failure.Contains(key, StringComparison.Ordinal));
}
