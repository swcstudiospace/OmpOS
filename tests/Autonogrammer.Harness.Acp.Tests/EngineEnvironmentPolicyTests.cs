using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>The pure rules of <see cref="EngineEnvironmentPolicy"/>: which inherited variables an allow-listed child keeps.</summary>
public sealed class EngineEnvironmentPolicyTests
{
    private const string Secret = "SECRET_TEST_VAR";

    /// <summary>Inherit mode keeps every variable, including a secret.</summary>
    [Fact]
    public void InheritAllowsEverything()
    {
        var policy = new EngineEnvironmentPolicy(EngineEnvironmentMode.Inherit);

        policy.Mode.ShouldBe(EngineEnvironmentMode.Inherit);
        policy.IsAllowed(Secret).ShouldBeTrue();
        policy.IsAllowed("DATABASE_PASSWORD").ShouldBeTrue();
        EngineEnvironmentPolicy.Inherit.IsAllowed(Secret).ShouldBeTrue();
        policy.Rejected([Secret, "PATH"]).ShouldBeEmpty();
    }

    /// <summary>Allowlist mode keeps the variables an engine needs to run, in both cases for the proxy variables.</summary>
    [Theory]
    [InlineData("PATH")]
    [InlineData("HOME")]
    [InlineData("USER")]
    [InlineData("LOGNAME")]
    [InlineData("LANG")]
    [InlineData("TERM")]
    [InlineData("TZ")]
    [InlineData("TMPDIR")]
    [InlineData("SHELL")]
    [InlineData("PI_CODING_AGENT_DIR")]
    [InlineData("NODE_EXTRA_CA_CERTS")]
    [InlineData("SSL_CERT_FILE")]
    [InlineData("SSL_CERT_DIR")]
    [InlineData("HTTP_PROXY")]
    [InlineData("HTTPS_PROXY")]
    [InlineData("NO_PROXY")]
    [InlineData("http_proxy")]
    [InlineData("https_proxy")]
    [InlineData("no_proxy")]
    public void AllowlistKeepsTheBuiltInNames(string name)
    {
        new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist).IsAllowed(name).ShouldBeTrue();
    }

    /// <summary>Allowlist mode keeps the <c>LC_*</c> and <c>OMP_*</c> families and nothing that merely resembles them.</summary>
    [Theory]
    [InlineData("LC_ALL", true)]
    [InlineData("LC_MESSAGES", true)]
    [InlineData("OMP_PROFILE", true)]
    [InlineData("OMP_ANYTHING_ELSE", true)]
    [InlineData("LCX", false)]
    [InlineData("LC", false)]
    [InlineData("OMPX_PROFILE", false)]
    [InlineData("OMP", false)]
    [InlineData("PATHX", false)]
    [InlineData("XPATH", false)]
    public void AllowlistKeepsTheBuiltInPrefixFamiliesOnly(string name, bool expected)
    {
        new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist).IsAllowed(name).ShouldBe(expected);
    }

    /// <summary>Allowlist mode drops secrets and anything else not on the list, and reports them in the order given.</summary>
    [Fact]
    public void AllowlistRejectsSecretsAndKeepsTheOrder()
    {
        var policy = new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist);

        policy.IsAllowed(Secret).ShouldBeFalse();
        string[] expected = ["DATABASE_PASSWORD", Secret];
        policy.Rejected(["PATH", "DATABASE_PASSWORD", "HOME", Secret, "OMP_X"]).ToArray().ShouldBe(expected);
    }

    /// <summary>Configured allow-list entries add exact names and, with a trailing star, prefixes.</summary>
    [Fact]
    public void AllowListEntriesAddExactNamesAndPrefixes()
    {
        var policy = new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist, ["ANTHROPIC_API_KEY", "OPENAI_*"]);

        policy.IsAllowed("ANTHROPIC_API_KEY").ShouldBeTrue();
        policy.IsAllowed("ANTHROPIC_API_KEY_2").ShouldBeFalse();
        policy.IsAllowed("OPENAI_API_KEY").ShouldBeTrue();
        policy.IsAllowed("OPENAI_").ShouldBeTrue();
        policy.IsAllowed("OPENAIKEY").ShouldBeFalse();
        policy.IsAllowed(Secret).ShouldBeFalse();
    }

    /// <summary>The allow-list is ignored in Inherit mode, where everything is already kept.</summary>
    [Fact]
    public void AllowListIsIgnoredInInheritMode()
    {
        new EngineEnvironmentPolicy(EngineEnvironmentMode.Inherit, ["ANTHROPIC_API_KEY"]).IsAllowed(Secret).ShouldBeTrue();
    }

    /// <summary>An unusable allow-list entry is refused, and the message does not echo it.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SECRET=value")]
    [InlineData("SECRET*MIDDLE")]
    [InlineData("*")]
    [InlineData("SECRET NAME")]
    [InlineData("SECRET\0NAME")]
    public void InvalidAllowListEntryIsRefusedWithoutEchoingIt(string entry)
    {
        EngineEnvironmentPolicy.IsValidAllowEntry(entry).ShouldBeFalse();

        var failure = Should.Throw<ArgumentException>(() => new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist, [entry]));

        failure.Message.ShouldNotContain("SECRET", Case.Sensitive);
    }

    /// <summary>A null entry and a usable entry are classified without throwing.</summary>
    [Fact]
    public void AllowEntryValidationClassifiesNullAndUsableEntries()
    {
        EngineEnvironmentPolicy.IsValidAllowEntry(null).ShouldBeFalse();
        EngineEnvironmentPolicy.IsValidAllowEntry("ANTHROPIC_API_KEY").ShouldBeTrue();
        EngineEnvironmentPolicy.IsValidAllowEntry("OPENAI_*").ShouldBeTrue();
    }

    /// <summary>An undefined mode is refused.</summary>
    [Fact]
    public void UndefinedModeIsRefused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new EngineEnvironmentPolicy((EngineEnvironmentMode)99));
    }

    /// <summary>Apply removes disallowed inherited variables and then sets the explicit ones, which win and are never filtered.</summary>
    [Fact]
    public void ApplyRemovesDisallowedVariablesThenSetsExplicitOnesLast()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            ["OMP_X"] = "inherited-omp",
            [Secret] = "inherited-secret",
            ["DATABASE_PASSWORD"] = "inherited-password",
        };
        var explicitVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Secret] = "explicit-secret",
            ["OMP_X"] = "explicit-omp",
            ["EXPLICIT_ONLY"] = "explicit-only",
        };

        new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist).Apply(environment, explicitVariables);

        environment.Count.ShouldBe(4);
        environment["PATH"].ShouldBe("/usr/bin");
        environment["OMP_X"].ShouldBe("explicit-omp");
        environment[Secret].ShouldBe("explicit-secret");
        environment["EXPLICIT_ONLY"].ShouldBe("explicit-only");
        environment.ContainsKey("DATABASE_PASSWORD").ShouldBeFalse();
    }

    /// <summary>Apply in Inherit mode keeps every inherited variable and still lets an explicit one override.</summary>
    [Fact]
    public void ApplyInInheritModeKeepsEverythingAndLetsExplicitVariablesOverride()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal) { ["HOME"] = "/home/user", [Secret] = "inherited-secret" };
        var explicitVariables = new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = "/override" };

        EngineEnvironmentPolicy.Inherit.Apply(environment, explicitVariables);

        environment["HOME"].ShouldBe("/override");
        environment[Secret].ShouldBe("inherited-secret");
    }
}
