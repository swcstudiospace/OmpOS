using Autonogrammer.Api.Cloud.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>Start-up validation of the Cloud section: nothing is checked while cloud is off; when it is on every missing or unusable setting is named by its key and no value is repeated.</summary>
public sealed class CloudOptionsValidatorTests
{
    private static readonly CloudOptionsValidator Validator = new();

    /// <summary>A host that leaves cloud off is never refused for what the section holds.</summary>
    [Fact]
    public void Disabled_IsNeverValidated()
    {
        var options = new CloudOptions { Environment = { Provider = "nonsense" }, Limits = { MaxAgentsPerOwner = -1 } };

        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    /// <summary>Enabling cloud with nothing else configured names the environment provider and the signing key.</summary>
    [Fact]
    public void Enabled_WithNothingElse_NamesTheMissingKeys()
    {
        var result = Validator.Validate(null, new CloudOptions { Enabled = true });

        result.Failed.ShouldBeTrue();
        Names(result, "Cloud:Environment:Provider").ShouldBeTrue();
        Names(result, "Cloud:Worker:SigningKeyPem").ShouldBeTrue();
    }

    /// <summary>A host with an external environment client and a valid key passes.</summary>
    [Fact]
    public void Enabled_WithExternalProviderAndKey_Passes()
    {
        using var keys = new CloudTestKeys();

        Validator.Validate(null, Valid(keys)).Succeeded.ShouldBeTrue();
    }

    /// <summary>Railway needs its token, project, environment, image and a worker public key; the failures name the keys and never the token that was set.</summary>
    [Fact]
    public void Railway_MissingSettings_AreNamedWithoutRepeatingValues()
    {
        using var keys = new CloudTestKeys();
        var options = Valid(keys);
        options.Environment.Provider = "Railway";
        options.Environment.Railway.ApiToken = "railway-token-value-should-not-leak";

        var result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        Names(result, "Cloud:Environment:Railway:ProjectId").ShouldBeTrue();
        Names(result, "Cloud:Environment:Railway:EnvironmentId").ShouldBeTrue();
        Names(result, "Cloud:Environment:Railway:Image").ShouldBeTrue();
        Names(result, "Cloud:Environment:Railway:WorkerPublicKeysPem").ShouldBeTrue();
        result.Failures!.Any(static failure => failure.Contains("railway-token-value-should-not-leak", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>A complete Railway section passes.</summary>
    [Fact]
    public void Railway_Complete_Passes()
    {
        using var keys = new CloudTestKeys();
        var options = Valid(keys);
        options.Environment.Provider = "Railway";
        options.Environment.Railway.ApiToken = "t";
        options.Environment.Railway.ProjectId = "p";
        options.Environment.Railway.EnvironmentId = "e";
        options.Environment.Railway.Image = "ghcr.io/swcstudiospace/autonogrammer-agent-worker:1";
        options.Environment.Railway.WorkerPublicKeysPem = [keys.WorkerPublicKeyPem];

        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    /// <summary>A static pool needs at least one worker with an absolute http or https address.</summary>
    [Fact]
    public void StaticPool_NeedsWorkersWithAddresses()
    {
        using var keys = new CloudTestKeys();
        var options = Valid(keys);
        options.Environment.Provider = "StaticPool";
        Names(Validator.Validate(null, options), "Cloud:Environment:StaticPool:Workers").ShouldBeTrue();

        options.Environment.StaticPool.Workers = [new CloudOptions.StaticPoolWorkerSettings { WorkerId = "w1", BaseUri = "not a uri" }];
        Names(Validator.Validate(null, options), "Cloud:Environment:StaticPool:Workers:0:BaseUri").ShouldBeTrue();

        options.Environment.StaticPool.Workers = [new CloudOptions.StaticPoolWorkerSettings { WorkerId = "w1", BaseUri = "http://w1.internal:8080/" }];
        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
        options.SupportsSessionVariables.ShouldBeFalse();
    }

    /// <summary>Limits that contradict each other are refused.</summary>
    [Fact]
    public void Limits_ThatContradict_AreRefused()
    {
        using var keys = new CloudTestKeys();
        var options = Valid(keys);
        options.Limits.MaxAgentsPerOwner = 2;
        options.Limits.MaxConcurrentRunningPerOwner = 3;
        options.Limits.DefaultIdleMinutes = 2000;

        var result = Validator.Validate(null, options);

        Names(result, "Cloud:Limits:MaxConcurrentRunningPerOwner").ShouldBeTrue();
        Names(result, "Cloud:Limits:DefaultIdleMinutes").ShouldBeTrue();
    }

    /// <summary>The documented defaults are five agents, two running and a fifteen minute idle window.</summary>
    [Fact]
    public void Defaults_AreFiveTwoAndFifteen()
    {
        var options = new CloudOptions();

        options.Limits.MaxAgentsPerOwner.ShouldBe(5);
        options.Limits.MaxConcurrentRunningPerOwner.ShouldBe(2);
        options.Limits.DefaultIdleMinutes.ShouldBe(15);
        options.Enabled.ShouldBeFalse();
        options.Worker.TokenLifetime.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
    }

    /// <summary>A token lifetime over fifteen minutes is refused: workers would refuse the token.</summary>
    [Fact]
    public void TokenLifetime_OverFifteenMinutes_IsRefused()
    {
        using var keys = new CloudTestKeys();
        var options = Valid(keys);
        options.Worker.TokenLifetime = TimeSpan.FromMinutes(16);

        Names(Validator.Validate(null, options), "Cloud:Worker:TokenLifetime").ShouldBeTrue();
    }

    /// <summary>A signing key that is not an EC P-256 private key is refused without printing it.</summary>
    [Fact]
    public void SigningKey_ThatIsPublic_IsRefused()
    {
        using var keys = new CloudTestKeys();
        var options = Valid(keys);
        options.Worker.SigningKeyPem = keys.WorkerPublicKeyPem;

        var result = Validator.Validate(null, options);

        Names(result, "Cloud:Worker:SigningKeyPem").ShouldBeTrue();
        result.Failures!.Any(failure => failure.Contains("BEGIN PUBLIC KEY", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>A GitHub App needs both its id and its key; a key that is not an RSA private key is refused.</summary>
    [Fact]
    public void GitApp_NeedsBothSettings()
    {
        using var keys = new CloudTestKeys();
        var options = Valid(keys);
        options.Git.App.AppId = "123";
        Names(Validator.Validate(null, options), "Cloud:Git:App:AppId").ShouldBeTrue();

        options.Git.App.PrivateKeyPem = keys.WorkerPublicKeyPem;
        Names(Validator.Validate(null, options), "Cloud:Git:App:PrivateKeyPem").ShouldBeTrue();

        options.Git.App.PrivateKeyPem = keys.AppKeyPem;
        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    /// <summary>Operator-defined secrets must have valid names and values long enough to be redacted.</summary>
    [Fact]
    public void StaticSecrets_AreChecked()
    {
        using var keys = new CloudTestKeys();
        var options = Valid(keys);
        options.Secrets.Static["AUTONOGRAMMER_BAD"] = "long-enough-value";
        options.Secrets.Static["SHORT_ONE"] = "abc";

        var result = Validator.Validate(null, options);

        result.Failures!.Count(static failure => failure.StartsWith("Cloud:Secrets:Static", StringComparison.Ordinal)).ShouldBe(2);
        result.Failures!.Any(static failure => failure.Contains("long-enough-value", StringComparison.Ordinal)).ShouldBeFalse();
    }

    private static CloudOptions Valid(CloudTestKeys keys) =>
        new() { Enabled = true, Environment = { Provider = "External" }, Worker = { SigningKeyPem = keys.SigningKeyPem } };

    private static bool Names(Microsoft.Extensions.Options.ValidateOptionsResult result, string key) =>
        result.Failures is { } failures && failures.Any(failure => failure.Contains(key, StringComparison.Ordinal));
}
