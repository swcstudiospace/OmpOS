using Autonogrammer.Harness.Acp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests;

/// <summary>The environment a stdio MCP server gets follows the same <see cref="EngineEnvironmentPolicy"/> as the <c>omp acp</c> child.</summary>
public sealed class McpStdioEnvironmentTests
{
    private static readonly KeyValuePair<string, string>[] Inherited =
    [
        new("PATH", "/usr/bin"),
        new("HOME", "/home/user"),
        new("OMP_PROFILE", "work"),
        new("LC_ALL", "C"),
        new("SECRET_TEST_VAR", "parent-secret"),
        new("EXTRA_ALLOWED_TEST_VAR", "extra"),
    ];

    /// <summary>Inherit mode with no variables sets nothing, as before the option existed.</summary>
    [Fact]
    public void InheritWithoutVariablesSetsNothing()
    {
        BuildEnvironment(Options()).ShouldBeNull();
    }

    /// <summary>Inherit mode passes the explicit variables through unchanged, a null value still unsetting a variable, and never reads this process's environment.</summary>
    [Fact]
    public void InheritPassesExplicitVariablesThrough()
    {
        var explicitVariables = new Dictionary<string, string?>(StringComparer.Ordinal) { ["ADDED"] = "value", ["REMOVED"] = null };

        var environment = BuildEnvironment(Options(explicitVariables));

        environment.ShouldNotBeNull();
        environment["ADDED"].ShouldBe("value");
        environment.ContainsKey("REMOVED").ShouldBeTrue();
        environment["REMOVED"].ShouldBeNull();
    }

    /// <summary>Allowlist mode keeps the runtime variables, drops the secret and sets the explicit variables last.</summary>
    [Fact]
    public void AllowlistKeepsRuntimeVariablesAndDropsSecrets()
    {
        var explicitVariables = new Dictionary<string, string?>(StringComparer.Ordinal) { ["OMP_PROFILE"] = "explicit", ["SERVER_KEY"] = "server-key" };

        var environment = BuildEnvironment(Options(explicitVariables, EngineEnvironmentMode.Allowlist));

        environment.ShouldNotBeNull();
        environment["PATH"].ShouldBe("/usr/bin");
        environment["HOME"].ShouldBe("/home/user");
        environment["LC_ALL"].ShouldBe("C");
        environment["OMP_PROFILE"].ShouldBe("explicit");
        environment["SERVER_KEY"].ShouldBe("server-key");
        environment.ContainsKey("SECRET_TEST_VAR").ShouldBeFalse();
        environment.ContainsKey("EXTRA_ALLOWED_TEST_VAR").ShouldBeFalse();
    }

    /// <summary>Allowlist mode keeps the configured additions and lets a null explicit value unset an allowed variable.</summary>
    [Fact]
    public void AllowlistKeepsConfiguredAdditionsAndHonoursNullUnsets()
    {
        var explicitVariables = new Dictionary<string, string?>(StringComparer.Ordinal) { ["HOME"] = null };

        var environment = BuildEnvironment(Options(explicitVariables, EngineEnvironmentMode.Allowlist, ["EXTRA_ALLOWED_TEST_VAR"]));

        environment.ShouldNotBeNull();
        environment["EXTRA_ALLOWED_TEST_VAR"].ShouldBe("extra");
        environment.ContainsKey("HOME").ShouldBeFalse();
        environment.ContainsKey("SECRET_TEST_VAR").ShouldBeFalse();
    }

    /// <summary>Allowlist mode with no explicit variables still yields the allowed subset, never null.</summary>
    [Fact]
    public void AllowlistWithoutExplicitVariablesStillRestrictsTheEnvironment()
    {
        var environment = BuildEnvironment(Options(null, EngineEnvironmentMode.Allowlist));

        environment.ShouldNotBeNull();
        environment.ContainsKey("SECRET_TEST_VAR").ShouldBeFalse();
        environment["PATH"].ShouldBe("/usr/bin");
    }

    /// <summary>An unusable allow-list entry is refused before any process starts.</summary>
    [Fact]
    public void InvalidAllowListEntryIsRefused()
    {
        Should.Throw<ArgumentException>(() => BuildEnvironment(Options(null, EngineEnvironmentMode.Allowlist, ["A=B"])));
    }

    /// <summary>The inherited environment is not read in Inherit mode.</summary>
    [Fact]
    public void InheritDoesNotReadTheProcessEnvironment()
    {
        var read = false;

        _ = McpClientConnector.BuildEnvironment(
            Options(),
            () =>
            {
                read = true;
                return Inherited;
            });

        read.ShouldBeFalse();
    }

    private static McpStdioServerOptions Options(
        IReadOnlyDictionary<string, string?>? variables = null,
        EngineEnvironmentMode mode = EngineEnvironmentMode.Inherit,
        IReadOnlyList<string>? allow = null) =>
        new("server", [], "/tmp", variables, mode, allow);

    private static Dictionary<string, string?>? BuildEnvironment(McpStdioServerOptions options) =>
        McpClientConnector.BuildEnvironment(options, () => Inherited);
}
