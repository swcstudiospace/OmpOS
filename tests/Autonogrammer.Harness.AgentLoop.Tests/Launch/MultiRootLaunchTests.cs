using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Autonogrammer.Harness.AgentLoop.Tests.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Launch;

/// <summary>
/// Verifies how the additional roots of a multi-root workspace reach omp: <c>--add-dir</c> arguments in order and unquoted, the
/// validation that keeps a bad path out of the argument list, the terminal command that inherits them and the overlay record.
/// </summary>
public sealed class MultiRootLaunchTests
{
    private const string Workspace = "/work/space";

    private static AgentLoopSettings Settings(bool disableProjectMcp = false) =>
        new() { PluginsRoot = PlannerFixtures.PluginsRoot, StateRoot = PlannerFixtures.StateRoot, DisableProjectMcpConfig = disableProjectMcp };

    private static OmpLaunchPlan Plan(AgentLoopSettings settings, IReadOnlyList<string>? additional, IReadOnlyList<ResolvedPlugin>? plugins = null) =>
        OmpLaunchPlanner.Plan(settings, plugins ?? [], "s1", Workspace, null, new FixedTimeProvider(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero)), additional);

    [Fact]
    public void ASingleRootSessionGetsNoAddDirArgumentAndNoRecordedDirectory()
    {
        var plan = Plan(Settings(), additional: null);

        plan.ExtraArguments.ShouldBe(["--no-extensions"]);
        plan.AdditionalDirectories.ShouldBeEmpty();
        plan.WorkingDirectory.ShouldBe(Workspace);
    }

    [Fact]
    public void AnEmptyListIsTheSameAsNull()
    {
        Plan(Settings(), additional: []).ExtraArguments.ShouldBe(Plan(Settings(), additional: null).ExtraArguments);
    }

    [Fact]
    public void EveryAdditionalRootBecomesItsOwnAddDirPairInOrderAfterThePluginArguments()
    {
        var plan = Plan(Settings(), ["/repos/web", "/repos/api", "/repos/docs"], [PlannerFixtures.Ready(OmpPluginCatalog.UltrathinkId)]);

        plan.ExtraArguments.ShouldBe(
        [
            "--no-extensions",
            "-e", "/p/claude-ultrathink",
            "--add-dir", "/repos/web",
            "--add-dir", "/repos/api",
            "--add-dir", "/repos/docs",
        ]);
        plan.AdditionalDirectories.ShouldBe(["/repos/web", "/repos/api", "/repos/docs"]);
    }

    [Fact]
    public void TheAddDirPairsFollowTheConfigOverlayArgument()
    {
        var plan = Plan(Settings(disableProjectMcp: true), ["/repos/web"]);

        plan.ExtraArguments.ShouldBe(
        [
            "--no-extensions",
            "--config", SessionStateLayout.ConfigOverlayFile("/state/sessions/s1"),
            "--add-dir", "/repos/web",
        ]);
    }

    [Fact]
    public void APathWithSpacesQuotesAndShellCharactersIsOneUntouchedArgument()
    {
        const string hostile = "/repos/my project/it's \"x\" $HOME `id`; rm -rf";

        var plan = Plan(Settings(), [hostile]);

        plan.ExtraArguments.ShouldBe(["--no-extensions", "--add-dir", hostile]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("repos/relative")]
    [InlineData("../sibling")]
    [InlineData("/repos/a\0b")]
    public void ABlankRelativeOrNulDirectoryIsRejected(string directory)
    {
        Should.Throw<ArgumentException>(() => Plan(Settings(), [directory]));
    }

    [Fact]
    public void ADirectoryThatRepeatsAnotherIsRejectedEvenWithATrailingSeparator()
    {
        Should.Throw<ArgumentException>(() => Plan(Settings(), ["/repos/web", "/repos/web/"]));
    }

    [Fact]
    public void TheWorkspaceRootItselfIsRejectedAsAnAdditionalDirectory()
    {
        Should.Throw<ArgumentException>(() => Plan(Settings(), ["/repos/web", Workspace + "/"]));
    }

    [Fact]
    public void ThePlannedDirectoriesAreACopyOfTheCallersList()
    {
        var caller = new List<string> { "/repos/web" };

        var plan = Plan(Settings(), caller);
        caller.Add("/repos/late");

        plan.AdditionalDirectories.ShouldBe(["/repos/web"]);
        plan.ExtraArguments.ShouldNotContain("/repos/late", StringComparer.Ordinal);
    }

    [Fact]
    public void TheTerminalCommandKeepsTheAddDirPairsAheadOfTheResumeReference()
    {
        var plan = Plan(Settings(), ["/repos/web"]);

        var command = plan.ForTerminal(new OmpTerminalOptions { NativeSessionId = "native-1" });

        command.Arguments.ShouldBe(["--no-extensions", "--add-dir", "/repos/web", "--resume", "native-1"]);
    }

    [Fact]
    public void TheOverlayRecordsTheDirectoriesAsQuotedYamlWhenAnOverlayIsWritten()
    {
        var text = ConfigOverlayWriter.Render(Settings(disableProjectMcp: true), ["/repos/web", "C:\\repos\\api", "/repos/q\"uote"]);

        text.ShouldBe(
            "mcp:\n  enableProjectConfig: false\n"
            + "workspace:\n  additionalDirectories:\n"
            + "    - \"/repos/web\"\n"
            + "    - \"C:\\\\repos\\\\api\"\n"
            + "    - \"/repos/q\\\"uote\"\n");
    }

    [Fact]
    public void TheOverlayHasNoWorkspaceKeyForASingleRootSession()
    {
        ConfigOverlayWriter.Render(Settings(disableProjectMcp: true), null).ShouldBe("mcp:\n  enableProjectConfig: false\n");
        ConfigOverlayWriter.Render(Settings(disableProjectMcp: true), []).ShouldBe("mcp:\n  enableProjectConfig: false\n");
    }

    [Fact]
    public void NoOverlayIsRenderedForDirectoriesAloneBecauseTheArgumentsCarryThem()
    {
        ConfigOverlayWriter.Render(Settings(disableProjectMcp: false), ["/repos/web"]).ShouldBeNull();
    }

    [Fact]
    public void TheWrittenOverlayFileHoldsTheDirectories()
    {
        using var fixture = new PluginFixtureDirectory();
        var sessionDirectory = fixture.PathOf("state/sessions/s1");

        using var writer = ConfigOverlayWriter.Write(Settings(disableProjectMcp: true), sessionDirectory, ["/repos/web"]);

        writer.ShouldNotBeNull();
        File.ReadAllText(writer.Path).ShouldContain("- \"/repos/web\"", Case.Sensitive);
    }
}
