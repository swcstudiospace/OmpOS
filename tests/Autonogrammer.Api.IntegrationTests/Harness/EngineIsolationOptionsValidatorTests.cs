using Autonogrammer.Api.Harness;
using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Harness;

/// <summary>
/// The start-up validation of the engine isolation keys: outside Development the engine gets an allow-listed environment by default, may not
/// inherit the API's environment without an explicit unsafe opt-in, and needs a working directory of its own.
/// </summary>
public sealed class EngineIsolationOptionsValidatorTests
{
    private static readonly string WorkspaceRoot = Path.Combine(Path.GetTempPath(), "autonogrammer-engine-workspaces");

    /// <summary>Production defaults: no mode configured resolves to Allowlist, and a workspace root alone is enough to start.</summary>
    [Fact]
    public void ProductionDefaultsToAllowlistAndNeedsOnlyAWorkspaceRoot()
    {
        var acp = new HarnessOptions.AcpSettings { WorkspaceRoot = WorkspaceRoot };

        Validate(TestHostEnvironment.Production, acp).Succeeded.ShouldBeTrue();
        EngineIsolationSettings.Resolve(acp, development: false).Mode.ShouldBe(EngineEnvironmentMode.Allowlist);
    }

    /// <summary>Development defaults to Inherit and needs neither a workspace root nor worktrees.</summary>
    [Fact]
    public void DevelopmentDefaultsToInheritWithNoWorkspaceRoot()
    {
        var acp = new HarnessOptions.AcpSettings();

        Validate(TestHostEnvironment.Development, acp).Succeeded.ShouldBeTrue();
        var settings = EngineIsolationSettings.Resolve(acp, development: true);
        settings.Mode.ShouldBe(EngineEnvironmentMode.Inherit);
        settings.WorkspaceRoot.ShouldBeNull();
    }

    /// <summary>An explicit mode wins over the environment default in both directions.</summary>
    [Fact]
    public void ExplicitModeWinsOverTheEnvironmentDefault()
    {
        var allowlist = new HarnessOptions.AcpSettings { Environment = new HarnessOptions.AcpEnvironmentSettings { Mode = EngineEnvironmentMode.Allowlist } };
        var inherit = new HarnessOptions.AcpSettings { Environment = new HarnessOptions.AcpEnvironmentSettings { Mode = EngineEnvironmentMode.Inherit } };

        EngineIsolationSettings.Resolve(allowlist, development: true).Mode.ShouldBe(EngineEnvironmentMode.Allowlist);
        EngineIsolationSettings.Resolve(inherit, development: false).Mode.ShouldBe(EngineEnvironmentMode.Inherit);
        Validate(TestHostEnvironment.Development, allowlist).Succeeded.ShouldBeTrue();
    }

    /// <summary>Production refuses Inherit unless Harness:Acp:AllowInheritedEnvironment is true, and the refusal names both keys.</summary>
    [Fact]
    public void ProductionRefusesInheritWithoutTheUnsafeOptIn()
    {
        var acp = new HarnessOptions.AcpSettings
        {
            WorkspaceRoot = WorkspaceRoot,
            Environment = new HarnessOptions.AcpEnvironmentSettings { Mode = EngineEnvironmentMode.Inherit },
        };

        var result = Validate(TestHostEnvironment.Production, acp);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Harness:Acp:Environment:Mode");
        result.FailureMessage.ShouldContain("Harness:Acp:AllowInheritedEnvironment");
    }

    /// <summary>Production accepts Inherit once the unsafe opt-in is set.</summary>
    [Fact]
    public void ProductionAcceptsInheritWithTheUnsafeOptIn()
    {
        var acp = new HarnessOptions.AcpSettings
        {
            WorkspaceRoot = WorkspaceRoot,
            AllowInheritedEnvironment = true,
            Environment = new HarnessOptions.AcpEnvironmentSettings { Mode = EngineEnvironmentMode.Inherit },
        };

        Validate(TestHostEnvironment.Production, acp).Succeeded.ShouldBeTrue();
    }

    /// <summary>Production without a workspace root and without both worktree keys fails and names Harness:Acp:WorkspaceRoot.</summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("/srv/worktrees", "")]
    [InlineData("", "/srv/repository")]
    public void ProductionWithoutAWorkingDirectoryOfItsOwnNamesTheWorkspaceRoot(string worktreeRoot, string worktreeRepository)
    {
        var result = Validate(TestHostEnvironment.Production, new HarnessOptions.AcpSettings(), new WorktreesOptions { Root = worktreeRoot, Repository = worktreeRepository });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Harness:Acp:WorkspaceRoot");
    }

    /// <summary>Production with both worktree keys needs no workspace root.</summary>
    [Fact]
    public void ProductionWithWorktreesNeedsNoWorkspaceRoot()
    {
        var result = Validate(TestHostEnvironment.Production, new HarnessOptions.AcpSettings(), new WorktreesOptions { Root = "/srv/worktrees", Repository = "/srv/repository" });

        result.Succeeded.ShouldBeTrue();
    }

    /// <summary>A relative workspace root fails in every environment and the message names the key.</summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void RelativeWorkspaceRootFailsNamingTheKey(string environment)
    {
        var acp = new HarnessOptions.AcpSettings { WorkspaceRoot = "relative/workspaces" };

        var result = Validate(new TestHostEnvironment(environment), acp);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Harness:Acp:WorkspaceRoot must be an absolute path");
    }

    /// <summary>An unusable allow-list entry fails and names its index but never echoes the entry.</summary>
    [Theory]
    [InlineData("SECRET=value")]
    [InlineData("")]
    [InlineData("A*B")]
    [InlineData("*")]
    public void UnusableAllowEntryFailsNamingItsIndexWithoutEchoingIt(string entry)
    {
        var acp = new HarnessOptions.AcpSettings
        {
            WorkspaceRoot = WorkspaceRoot,
            Environment = new HarnessOptions.AcpEnvironmentSettings { Allow = ["ANTHROPIC_API_KEY", entry] },
        };

        var result = Validate(TestHostEnvironment.Production, acp);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Harness:Acp:Environment:Allow:1");
        result.FailureMessage.ShouldNotContain("Allow:0");
        if (entry.Length > 1)
        {
            result.FailureMessage.ShouldNotContain(entry, Case.Sensitive);
        }
    }

    /// <summary>Usable allow-list entries pass and reach the resolved settings.</summary>
    [Fact]
    public void UsableAllowEntriesPassAndAreResolved()
    {
        var acp = new HarnessOptions.AcpSettings
        {
            WorkspaceRoot = WorkspaceRoot,
            Environment = new HarnessOptions.AcpEnvironmentSettings { Allow = ["ANTHROPIC_API_KEY", "OPENAI_*"] },
        };

        Validate(TestHostEnvironment.Production, acp).Succeeded.ShouldBeTrue();
        string[] expected = ["ANTHROPIC_API_KEY", "OPENAI_*"];
        EngineIsolationSettings.Resolve(acp, development: false).Allow.ToArray().ShouldBe(expected);
    }

    /// <summary>A blank workspace root resolves to unset rather than to the empty path.</summary>
    [Fact]
    public void BlankWorkspaceRootResolvesToUnset()
    {
        EngineIsolationSettings.Resolve(new HarnessOptions.AcpSettings { WorkspaceRoot = "  " }, development: true).WorkspaceRoot.ShouldBeNull();
    }

    /// <summary>The isolation keys are not checked when the in-memory adapter is selected; its own rule refuses it outside Development.</summary>
    [Fact]
    public void InMemoryAdapterIsNotCheckedForIsolation()
    {
        var options = new HarnessOptions { Adapter = HarnessOptions.InMemoryAdapter };

        new EngineIsolationOptionsValidator(TestHostEnvironment.Production, Microsoft.Extensions.Options.Options.Create(new WorktreesOptions()))
            .Validate(null, options).Succeeded.ShouldBeTrue();
    }

    /// <summary>AddEngineIsolation registers the validator for the harness options.</summary>
    [Fact]
    public void AddEngineIsolationRegistersTheValidator()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new WorktreesOptions()));

        using var provider = services.AddEngineIsolation(TestHostEnvironment.Production).BuildServiceProvider();

        var validator = provider.GetRequiredService<IValidateOptions<HarnessOptions>>();
        validator.ShouldBeOfType<EngineIsolationOptionsValidator>();
        validator.Validate(null, new HarnessOptions { Adapter = HarnessOptions.AcpAdapter }).Failed.ShouldBeTrue();
    }

    private static ValidateOptionsResult Validate(IHostEnvironment environment, HarnessOptions.AcpSettings acp, WorktreesOptions? worktrees = null)
    {
        var validator = new EngineIsolationOptionsValidator(environment, Microsoft.Extensions.Options.Options.Create(worktrees ?? new WorktreesOptions()));
        return validator.Validate(null, new HarnessOptions { Adapter = HarnessOptions.AcpAdapter, Acp = acp });
    }
}
