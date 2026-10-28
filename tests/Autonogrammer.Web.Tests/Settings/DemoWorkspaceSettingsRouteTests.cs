using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Web.Services.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests.Settings;

/// <summary>The read-only workspace settings routes of the Demo handler, read through the same client the web head uses.</summary>
public sealed class DemoWorkspaceSettingsRouteTests : IDisposable
{
    private readonly DemoTestHost _host = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The conventions of the acme-shop workspace: two rules, three skills, two commands, one subagent, a hook file, two MCP files and the problems found.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ConventionsHoldTheAcmeShopSet()
    {
        var conventions = await Client().GetConventionsAsync(Token).ConfigureAwait(true);

        conventions.Available.ShouldBeTrue();
        conventions.Rules.Select(static rule => rule.Name).ShouldBe(["code-style", "AGENTS.md"]);
        conventions.Skills.Select(static skill => skill.Name).ShouldBe(["review-pr", "release-notes", "explain-error"]);
        conventions.Skills.Select(static skill => skill.Scope).ShouldBe([ConventionScopeKind.Project, ConventionScopeKind.Project, ConventionScopeKind.User]);
        conventions.Commands.Select(static command => command.Name).ShouldBe(["test-product", "changelog"]);
        conventions.Subagents.Select(static agent => agent.Name).ShouldBe(["verifier"]);
        conventions.HookFiles.Select(static file => file.Origin).ShouldBe([".cursor/hooks.json"]);
        conventions.McpFiles.Select(static file => file.Origin).ShouldBe([".cursor/mcp.json", ".mcp.json"]);
        conventions.Diagnostics.ShouldContain(static problem => problem.Item == ConventionItemType.Config && problem.Code == ConventionDiagnosticCode.Malformed && problem.Origin == ".mcp.json");
    }

    /// <summary>No convention item sends an absolute server path: every origin is workspace-relative or starts with <c>~/</c>.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task OriginsAreRelativeAndNeverServerPaths()
    {
        var conventions = await Client().GetConventionsAsync(Token).ConfigureAwait(true);

        var origins = conventions.Rules.Select(static rule => rule.Origin)
            .Concat(conventions.Skills.Select(static skill => skill.Origin))
            .Concat(conventions.Commands.Select(static command => command.Origin))
            .Concat(conventions.Subagents.Select(static agent => agent.Origin))
            .Concat(conventions.McpFiles.Select(static file => file.Origin))
            .Concat(conventions.HookFiles.Select(static file => file.Origin));
        origins.ShouldAllBe(static origin => !origin.StartsWith('/') && !origin.Contains(':', StringComparison.Ordinal));
    }

    /// <summary>The hooks route lists three hooks with their file, one of them disabled, and a problem; the runtime values are the API defaults.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task HooksAndRuntimeAnswerWithFixtures()
    {
        var hooks = await Client().GetHooksAsync(Token).ConfigureAwait(true);
        var runtime = await Client().GetRuntimeAsync(Token).ConfigureAwait(true);

        hooks.Available.ShouldBeTrue();
        hooks.Hooks.Select(static hook => hook.EventName).ShouldBe(["beforeShellExecution", "afterFileEdit", "stop"]);
        hooks.Hooks.Single(static hook => !hook.Enabled).EventName.ShouldBe("stop");
        hooks.Sources.Single().File.ShouldBe(".cursor/hooks.json");
        hooks.Diagnostics.Single().Severity.ShouldBe("warning");
        runtime.RunLeaseSeconds.ShouldBe(30);
        runtime.RunLeaseRenewSeconds.ShouldBe(10);
        runtime.MaxPromptLength.ShouldBe(64000);
        runtime.WorkspaceConfigured.ShouldBeTrue();
    }

    /// <summary>The plugins route lists the four connected plugins; the network route lists endpoints without claiming a check; automations are unavailable with a reason.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task PluginsNetworkAndAutomationsAnswerHonestly()
    {
        var plugins = await Client().GetPluginsAsync(Token).ConfigureAwait(true);
        var network = await Client().GetNetworkAsync(Token).ConfigureAwait(true);
        var automations = await Client().GetAutomationsAsync(Token).ConfigureAwait(true);

        plugins.Plugins.Count.ShouldBe(4);
        plugins.Plugins.ShouldAllBe(static plugin => plugin.Found);
        network.Checked.ShouldBeFalse();
        network.Reason.ShouldContain("no network request", Case.Insensitive);
        network.Endpoints.ShouldNotBeEmpty();
        automations.Available.ShouldBeFalse();
        automations.Reason.ShouldNotBeNullOrWhiteSpace();
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private WorkspaceSettingsClient Client() => WorkspaceFixtures.Client(_host.Http);
}
