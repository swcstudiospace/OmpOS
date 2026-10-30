using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Plugins;

/// <summary>
/// Verifies the built-in plugin table: composition order, directories, entries and the submodule each plugin lives in.
/// </summary>
public sealed class OmpPluginCatalogTests
{
    [Fact]
    public void BuiltInListsTheFourPluginsInCompositionOrder()
    {
        OmpPluginCatalog.BuiltIn.Select(plugin => plugin.Id).ShouldBe(["ultrathink", "swarm", "substrate", "gsd"]);
        OmpPluginCatalog.BuiltIn.Select(plugin => plugin.Role).ShouldBe(
        [
            OmpPluginRole.PromptPlanner,
            OmpPluginRole.SubagentOrchestrator,
            OmpPluginRole.SessionSubstrate,
            OmpPluginRole.WorkflowEngine,
        ]);
    }

    [Fact]
    public void BuiltInDirectoriesAndEntriesMatchTheSubmoduleLayout()
    {
        OmpPluginCatalog.BuiltIn.Select(plugin => plugin.RelativeDirectory).ShouldBe(
        [
            "claude-ultrathink",
            "agent-swarm/omp",
            "agent-substrate/packages/projector/omp",
            "gsd-core-omp",
        ]);
        OmpPluginCatalog.BuiltIn.Single(plugin => string.Equals(plugin.Id, "substrate", StringComparison.Ordinal)).EntryRelativePaths
            .ShouldBe(["substrate-status.ts", "substrate-skills.ts"]);
        OmpPluginCatalog.BuiltIn.Where(plugin => !string.Equals(plugin.Id, "substrate", StringComparison.Ordinal))
            .ShouldAllBe(plugin => plugin.EntryRelativePaths.Count == 0);
    }

    [Fact]
    public void FixCommandsNameTheSubmoduleRootNotTheNestedDirectory()
    {
        OmpPluginCatalog.BuiltIn.Select(plugin => plugin.OptionalFixCommand).ShouldBe(
        [
            "git submodule update --init --recursive plugins/claude-ultrathink",
            "git submodule update --init --recursive plugins/agent-swarm",
            "git submodule update --init --recursive plugins/agent-substrate",
            "git restore plugins/gsd-core-omp",
        ]);
        OmpPluginCatalog.BuiltIn.Select(plugin => plugin.SubmoduleName).ShouldBe(
            ["claude-ultrathink", "agent-swarm", "agent-substrate", null]);
    }

    [Fact]
    public void EachPluginReportsAsItsOwnStageSource()
    {
        OmpPluginCatalog.BuiltIn.Select(plugin => plugin.StageSource).ShouldBe(
            [StageSource.Ultrathink, StageSource.Swarm, StageSource.Substrate, StageSource.Gsd]);
    }

    [Fact]
    public void EachPluginListsTheNamesItsSourceRegisters()
    {
        var byId = OmpPluginCatalog.BuiltIn.ToDictionary(plugin => plugin.Id, StringComparer.Ordinal);

        byId["ultrathink"].ExpectedCommands.ShouldBe(
        [
            "ultrathink-quick",
            "ultrathink-skip",
            "ultrathink-off",
            "ultrathink-on",
            "ultrathink-track",
            "ultrathink-status",
            "ultrathink-ui",
            "ultrathink-swarm",
        ]);
        byId["ultrathink"].ExpectedTools.ShouldBeEmpty();
        byId["swarm"].ExpectedCommands.ShouldBe(["swarm"]);
        byId["swarm"].ExpectedTools.ShouldBe(
        [
            "swarm_status",
            "swarm_assignment",
            "swarm_plan",
            "swarm_ingest",
            "swarm_transition",
            "swarm_gate",
            "swarm_catalog",
            "swarm_tool_call",
        ]);
        byId["substrate"].ExpectedCommands.ShouldBe(["substrate"]);
        byId["substrate"].ExpectedTools.ShouldBeEmpty();
        byId["gsd"].ExpectedCommands.ShouldBe(["gsd-core"]);
        byId["gsd"].ExpectedTools.ShouldBeEmpty();
    }

    [Fact]
    public void ExpectedNamesAreUniqueAndCarryNoSlashOrWhitespace()
    {
        var names = OmpPluginCatalog.BuiltIn.SelectMany(plugin => plugin.ExpectedCommands.Concat(plugin.ExpectedTools)).ToList();

        names.Distinct(StringComparer.Ordinal).Count().ShouldBe(names.Count);
        names.ShouldAllBe(name => name.Length > 0 && !name.StartsWith('/') && !name.Any(char.IsWhiteSpace));
    }
}
