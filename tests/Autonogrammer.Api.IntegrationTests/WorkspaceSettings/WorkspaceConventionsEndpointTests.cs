using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Api.Services.WorkspaceSettings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary><c>GET /workspace/conventions</c>: every convention directory, precedence, diagnostics, the user-scope switch and the absence of server paths.</summary>
public sealed class WorkspaceConventionsEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A host with no workspace folder says so instead of answering an empty list that reads as "no conventions".</summary>
    [Fact]
    public async Task WithoutAWorkspaceFolder_IsUnavailableWithAReason()
    {
        using var factory = WorkspaceHost.Dev(null);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/conventions", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("available").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("reason").GetString().ShouldNotBeNull().ShouldContain("Api:Workspace:Root", Case.Sensitive);
        document.RootElement.GetProperty("commands").GetArrayLength().ShouldBe(0);
        document.RootElement.GetProperty("includesUserScope").GetBoolean().ShouldBeFalse();
    }

    /// <summary>A configured folder that does not exist is unavailable too.</summary>
    [Fact]
    public async Task WithAMissingWorkspaceFolder_IsUnavailable()
    {
        using var workspace = new TempWorkspace();
        var missing = Path.Combine(workspace.Root, "gone");
        using var factory = WorkspaceHost.Dev(null, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Workspace:Root"] = missing });
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/conventions", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("available").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("reason").GetString().ShouldNotBeNull().ShouldContain("does not exist", Case.Sensitive);
    }

    /// <summary>Each of the six convention directories is read, with its precedence rank, project scope and a workspace-relative origin.</summary>
    [Theory]
    [InlineData(".autonogrammer", 0)]
    [InlineData(".omp", 1)]
    [InlineData(".cursor", 2)]
    [InlineData(".claude", 3)]
    [InlineData(".codex", 4)]
    [InlineData(".agents", 5)]
    public async Task EachConventionDirectory_IsRead_WithItsRank(string directory, int rank)
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write($"{directory}/commands/ship.md", "---\ndescription: Ship it\n---\nDeploy to $ARGUMENTS\n");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/conventions", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("available").GetBoolean().ShouldBeTrue();
        var command = document.RootElement.GetProperty("commands").EnumerateArray().ShouldHaveSingleItem();
        command.GetProperty("name").GetString().ShouldBe("ship");
        command.GetProperty("description").GetString().ShouldBe("Ship it");
        command.GetProperty("template").GetString().ShouldNotBeNull().ShouldContain("Deploy to $ARGUMENTS", Case.Sensitive);
        command.GetProperty("scope").GetString().ShouldBe("project");
        command.GetProperty("sourceDirectory").GetString().ShouldBe(directory);
        command.GetProperty("directoryRank").GetInt32().ShouldBe(rank);
        command.GetProperty("origin").GetString().ShouldBe($"{directory}/commands/ship.md");
    }

    /// <summary>The same command name in two directories lists the higher-precedence one and records the other as a duplicate diagnostic.</summary>
    [Fact]
    public async Task SameNameInTwoDirectories_ListsTheWinner_AndTheLoserIsADiagnostic()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write(".cursor/commands/deploy.md", "---\ndescription: from cursor\n---\nCursor body\n");
        _ = workspace.Write(".autonogrammer/commands/deploy.md", "---\ndescription: from autonogrammer\n---\nAutonogrammer body\n");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/conventions", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        var command = document.RootElement.GetProperty("commands").EnumerateArray().ShouldHaveSingleItem();
        command.GetProperty("sourceDirectory").GetString().ShouldBe(".autonogrammer");
        command.GetProperty("description").GetString().ShouldBe("from autonogrammer");
        var diagnostic = document.RootElement.GetProperty("diagnostics").EnumerateArray().ShouldHaveSingleItem();
        diagnostic.GetProperty("code").GetString().ShouldBe("duplicate");
        diagnostic.GetProperty("item").GetString().ShouldBe("command");
        diagnostic.GetProperty("origin").GetString().ShouldBe(".cursor/commands/deploy.md");
        diagnostic.GetProperty("reason").GetString().ShouldNotBeNull().ShouldContain(".autonogrammer/commands/deploy.md", Case.Sensitive);
    }

    /// <summary>Skills, rules, instruction files, subagents and the hooks and MCP files are listed with their directory and rank.</summary>
    [Fact]
    public async Task SkillsRulesSubagentsAndConfigFiles_AreListed()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write(".claude/skills/lint/SKILL.md", "---\nname: lint\ndescription: Lint the code\n---\nRun the linter\n");
        _ = workspace.Write(".cursor/rules/style.mdc", "---\ndescription: House style\nalwaysApply: false\n---\nUse tabs\n");
        _ = workspace.Write("AGENTS.md", "Be brief.\n");
        _ = workspace.Write(".codex/agents/reviewer.md", "---\ndescription: Reviews code\nreadonly: true\n---\nReview carefully\n");
        _ = workspace.Write(".cursor/hooks.json", "{ \"version\": 1, \"hooks\": {} }");
        _ = workspace.Write(".mcp.json", "{}");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/conventions", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        var root = document.RootElement;
        var skill = root.GetProperty("skills").EnumerateArray().ShouldHaveSingleItem();
        skill.GetProperty("name").GetString().ShouldBe("lint");
        skill.GetProperty("sourceDirectory").GetString().ShouldBe(".claude");
        skill.GetProperty("directoryRank").GetInt32().ShouldBe(3);
        skill.GetProperty("userInvocable").GetBoolean().ShouldBeTrue();
        var rules = root.GetProperty("rules").EnumerateArray().ToArray();
        rules.Select(static rule => rule.GetProperty("name").GetString()).ShouldBe(["AGENTS.md", "style"], ignoreOrder: true);
        var style = rules.Single(static rule => string.Equals(rule.GetProperty("name").GetString(), "style", StringComparison.Ordinal));
        style.GetProperty("type").GetString().ShouldBe("mdc");
        style.GetProperty("alwaysApply").GetBoolean().ShouldBeFalse();
        style.GetProperty("directoryRank").GetInt32().ShouldBe(2);
        var instructions = rules.Single(static rule => string.Equals(rule.GetProperty("name").GetString(), "AGENTS.md", StringComparison.Ordinal));
        instructions.GetProperty("type").GetString().ShouldBe("agents-file");
        instructions.GetProperty("alwaysApply").GetBoolean().ShouldBeTrue();
        instructions.TryGetProperty("directoryRank", out _).ShouldBeFalse();
        var agent = root.GetProperty("subagents").EnumerateArray().ShouldHaveSingleItem();
        agent.GetProperty("name").GetString().ShouldBe("reviewer");
        agent.GetProperty("readOnly").GetBoolean().ShouldBeTrue();
        agent.GetProperty("directoryRank").GetInt32().ShouldBe(4);
        agent.GetProperty("instructions").GetString().ShouldNotBeNull().ShouldContain("Review carefully", Case.Sensitive);
        root.GetProperty("hookFiles").EnumerateArray().ShouldHaveSingleItem().GetProperty("origin").GetString().ShouldBe(".cursor/hooks.json");
        root.GetProperty("mcpFiles").EnumerateArray().ShouldHaveSingleItem().GetProperty("origin").GetString().ShouldBe(".mcp.json");
    }

    /// <summary>With the switch at its default, a command in the process home is not read; with it on, it is listed as a user item with a <c>~/</c> origin.</summary>
    [Fact]
    public async Task UserScope_IsOffByDefault_AndReadWhenSwitchedOn()
    {
        using var workspace = new TempWorkspace();
        var home = workspace.NewFolder();
        _ = workspace.Write(".cursor/commands/home.md", "From the home.\n", home);
        void UseHome(IServiceCollection services)
        {
            _ = services.AddSingleton<IUserHomeProvider>(new FixedUserHome(home));
        }

        using (var off = WorkspaceHost.Dev(workspace, more: UseHome))
        using (var client = off.CreateClient())
        using (var response = await WorkspaceHost.GetAsync(client, "/workspace/conventions", Token).ConfigureAwait(true))
        using (var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true))
        {
            document.RootElement.GetProperty("includesUserScope").GetBoolean().ShouldBeFalse();
            document.RootElement.GetProperty("commands").GetArrayLength().ShouldBe(0);
        }

        var on = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Workspace:IncludeUserScope"] = "true" };
        using var factory = WorkspaceHost.Dev(workspace, on, UseHome);
        using var onClient = factory.CreateClient();
        using var onResponse = await WorkspaceHost.GetAsync(onClient, "/workspace/conventions", Token).ConfigureAwait(true);
        using var onDocument = await WebAppTestHost.ReadJsonAsync(onResponse, Token).ConfigureAwait(true);
        onDocument.RootElement.GetProperty("includesUserScope").GetBoolean().ShouldBeTrue();
        var command = onDocument.RootElement.GetProperty("commands").EnumerateArray().ShouldHaveSingleItem();
        command.GetProperty("name").GetString().ShouldBe("home");
        command.GetProperty("scope").GetString().ShouldBe("user");
        command.GetProperty("origin").GetString().ShouldBe("~/.cursor/commands/home.md");
    }

    /// <summary>A file over the discovery size cap is a diagnostic, and no server path appears anywhere in the body.</summary>
    [Fact]
    public async Task AnOversizeFile_IsADiagnostic_AndNoServerPathAppears()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write(".cursor/commands/big.md", new string('a', (256 * 1024) + 1));
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/conventions", Token).ConfigureAwait(true);

        var body = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("commands").GetArrayLength().ShouldBe(0);
        var diagnostic = document.RootElement.GetProperty("diagnostics").EnumerateArray().ShouldHaveSingleItem();
        diagnostic.GetProperty("code").GetString().ShouldBe("too-large");
        diagnostic.GetProperty("origin").GetString().ShouldBe(".cursor/commands/big.md");
        WorkspaceHost.ShouldNotLeakPaths(body, workspace);
    }

    /// <summary>A convention directory that is a link leaving the workspace is refused with a diagnostic and its files are not read.</summary>
    [Fact]
    public async Task ALinkedConventionDirectoryThatLeavesTheWorkspace_IsRefused()
    {
        using var workspace = new TempWorkspace();
        var outside = workspace.NewFolder();
        _ = workspace.Write("commands/leak.md", "Outside text.\n", outside);
        if (!workspace.TryLink(".cursor", outside))
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/conventions", Token).ConfigureAwait(true);

        var body = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("commands").GetArrayLength().ShouldBe(0);
        body.ShouldNotContain("Outside text", Case.Sensitive);
        WorkspaceHost.ShouldNotLeakPaths(body, workspace);
    }
}
