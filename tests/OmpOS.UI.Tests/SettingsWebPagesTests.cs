using Bunit;
using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Presentation.Settings;
using OmpOS.UI.Components.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The read-only pages of the web head (Runtime values, Hooks, MCP files, information pages): what each page model reads and what each view draws, including loading, error and empty states.</summary>
public sealed class SettingsWebPagesTests : UiTestContext
{
    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    /// <summary>The Runtime page reads the values once and groups them; an unreachable service puts it in its error state with the reason.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task RuntimePageGroupsTheValuesAndReportsAFailure()
    {
        var reader = new FakeReader();
        var page = new RuntimeValuesPage(reader);
        page.State.ShouldBe(SettingsPageState.Loading);
        page.Groups.ShouldBeEmpty();

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.IsDirty.ShouldBeFalse();
        page.Groups.SelectMany(static group => group.Rows).Single(static row => string.Equals(row.Label, "Largest request body", StringComparison.Ordinal)).Value.ShouldBe("1,048,576 bytes");
        page.Groups.SelectMany(static group => group.Rows).Single(static row => string.Equals(row.Label, "Lease time", StringComparison.Ordinal)).Value.ShouldBe("30 s");

        reader.Failure = "The server could not be reached.";
        await page.LoadAsync(Token).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("The server could not be reached.");
    }

    /// <summary>The Runtime view shows every group as a list of labelled values, no input, and an error state with a Try again button that loads again.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task RuntimeViewDrawsValuesOnlyAndRetries()
    {
        var reader = new FakeReader { Failure = "offline" };
        var page = new RuntimeValuesPage(reader);
        await page.LoadAsync(Token).ConfigureAwait(true);
        var cut = Render<RuntimeValuesView>(parameters => parameters.Add(p => p.Page, page));

        cut.Find("[role='alert']").TextContent.ShouldBe("offline");
        reader.Failure = null;
        await cut.Find("button").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.FindAll(".ompos-settings-list-row").Count.ShouldBe(page.Groups.Sum(static group => group.Rows.Count))).ConfigureAwait(true);
        cut.FindAll("input, select, textarea").Count.ShouldBe(0);
        cut.FindAll("h4").Select(static heading => heading.TextContent).ShouldBe(["Engine and stores", "Run lease", "Request limits", "Workspace"]);
    }

    /// <summary>The Hooks page lists the hooks with their command and detail; a service that has no workspace is an error with its own reason; no hook is an empty state.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task HooksPageListsHooksAndExplainsAnUnavailableService()
    {
        var reader = new FakeReader();
        var page = new HooksListPage(reader);

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.Hooks.Count.ShouldBe(2);
        page.IsEmpty.ShouldBeFalse();
        HooksListPage.CommandText(page.Hooks[0]).ShouldBe("npx prettier --write .");
        HooksListPage.CommandText(page.Hooks[1]).ShouldBe("./audit.sh");
        var cut = Render<HooksListView>(parameters => parameters.Add(p => p.Page, page));
        cut.FindAll(".ompos-hook-row").Count.ShouldBe(2);
        cut.FindAll(".ompos-hook-row")[1].TextContent.ShouldContain("disabled", Case.Insensitive);
        cut.FindAll(".ompos-hook-row")[0].TextContent.ShouldContain("from .cursor/hooks.json", Case.Sensitive);
        cut.Find(".ompos-settings-problems").TextContent.ShouldContain("warning: .cursor/hooks.json", Case.Sensitive);
        cut.FindAll("button, input").Count.ShouldBe(0, "the page lists; it runs and changes nothing");

        reader.HooksUnavailable = "No workspace is configured.";
        await page.LoadAsync(Token).ConfigureAwait(true);
        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("No workspace is configured.");

        reader.HooksUnavailable = null;
        reader.NoHooks = true;
        await page.LoadAsync(Token).ConfigureAwait(true);
        page.IsEmpty.ShouldBeTrue();
        Render<HooksListView>(parameters => parameters.Add(p => p.Page, page)).Markup.ShouldContain(SettingsWebCopy.HooksEmpty, Case.Sensitive);
    }

    /// <summary>The MCP page lists the MCP files with their scope and keeps only the problems of configuration files.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task McpPageListsFilesAndOnlyConfigProblems()
    {
        var page = new McpFilesPage(new FakeReader());

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.Files.Select(static file => file.Origin).ShouldBe([".cursor/mcp.json", "~/.cursor/mcp.json"]);
        page.Problems.Select(static problem => problem.Origin).ShouldBe([".cursor/mcp.json"]);
        var cut = Render<McpFilesView>(parameters => parameters.Add(p => p.Page, page));
        cut.FindAll(".ompos-settings-list-row .ompos-chip").Select(static chip => chip.TextContent).ShouldBe(["Project", "User"]);
        cut.Find(".ompos-settings-problems").TextContent.ShouldBe(".cursor/mcp.json: not valid JSON");
    }

    /// <summary>An information page draws its facts as headed paragraphs, the desktop notice only when it has one, and no control.</summary>
    [Fact]
    public void InfoViewDrawsFactsAndTheDesktopNotice()
    {
        var withNotice = new InfoSettingsPage(SettingsSection.Remote, "Remote environments", "Run elsewhere.", [new InfoSettingsFact("Why", "No ssh in a browser."), new InfoSettingsFact("Where", "In the desktop app.")], "Set it up there.");
        var without = new InfoSettingsPage(SettingsSection.Privacy, "Privacy", "Nothing to switch.", [new InfoSettingsFact("Data", "Stays here.")], null);

        var first = Render<InfoSettingsView>(parameters => parameters.Add(p => p.Page, withNotice));
        var second = Render<InfoSettingsView>(parameters => parameters.Add(p => p.Page, without));

        first.Find("h3").TextContent.ShouldBe("Remote environments");
        first.Find("[data-desktop-only='true']").TextContent.Trim().ShouldBe("Configure in the desktop app. Set it up there.");
        first.FindAll("h4").Select(static heading => heading.TextContent).ShouldBe(["Why", "Where"]);
        first.FindAll("[role='group']").Select(static group => group.GetAttribute("aria-labelledby")).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        first.FindAll("button, input, select, textarea").Count.ShouldBe(0);
        second.FindAll("[data-desktop-only]").Count.ShouldBe(0);
    }

    /// <summary>The dialog draws each web page kind with its own view, not the generic fallback.</summary>
    [Fact]
    public void TheDialogDrawsEveryWebPageKindWithItsOwnView()
    {
        JSInterop.SetupModule("./_content/OmpOS.UI/scripts/ompos-dialog.js").Setup<int>("trapFocus", _ => true).SetResult(1);
        var reader = new FakeReader();
        var info = new InfoSettingsPage(SettingsSection.Cloud, "Cloud agents", "Hosted.", [new InfoSettingsFact("A", "B")], "There.");
        var window = new SettingsWindowModel([info, new RuntimeValuesPage(reader), new HooksListPage(reader), new McpFilesPage(reader)]);

        var cut = Render<SettingsDialog>(parameters => parameters.Add(p => p.Settings, window).Add(p => p.IsOpen, true));

        cut.FindAll(".ompos-settings-state").Count.ShouldBe(0, "no page fell back to the generic view");
        cut.Find("h3").TextContent.ShouldBe("Cloud agents");
        foreach (var title in new[] { "Runtime", "Hooks", "MCP" })
        {
            cut.FindAll("nav button").Single(button => string.Equals(button.TextContent.Trim(), title, StringComparison.Ordinal)).Click();
            cut.WaitForAssertion(() => cut.FindAll("[data-state]").Count.ShouldBe(0, title));
            cut.WaitForAssertion(() => cut.Find("h3").TextContent.Trim().ShouldNotBeNullOrWhiteSpace());
        }
    }

    private sealed class FakeReader : IWorkspaceSettingsReader
    {
        internal string? Failure { get; set; }

        internal string? HooksUnavailable { get; set; }

        internal bool NoHooks { get; set; }

        public Task<RuntimeSettingsResponse> GetRuntimeAsync(CancellationToken cancellationToken) =>
            Failure is { } failure
                ? Task.FromException<RuntimeSettingsResponse>(new SettingsSourceException(failure))
                : Task.FromResult(new RuntimeSettingsResponse
                {
                    HarnessAdapter = "acp",
                    SessionStore = "postgres",
                    CoordinationStore = "dragonfly",
                    RunLeaseSeconds = 30,
                    RunLeaseRenewSeconds = 10,
                    MaxPromptLength = 64000,
                    MaxRequestBodyBytes = 1048576,
                    PromptsPerMinute = 60,
                    SessionsPerMinute = 60,
                    WorkspaceRequestsPerMinute = 120,
                    WorkspaceConfigured = true,
                    IncludesUserScope = false,
                    MaxWorkspaceFileBytes = 1048576,
                });

        public Task<HooksResponse> GetHooksAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new HooksResponse
            {
                Available = HooksUnavailable is null,
                Reason = HooksUnavailable,
                ConsentRecorded = false,
                Hooks = NoHooks ? [] : [Hook("a", "afterFileEdit", null, ["npx", "prettier", "--write", "."], enabled: true), Hook("b", "stop", "./audit.sh", null, enabled: false)],
                Sources = [new WorkspaceHookSourceInfo { Scope = "project", File = ".cursor/hooks.json", Format = "cursor", Priority = 0 }],
                Diagnostics = [new WorkspaceHookDiagnosticInfo { Severity = "warning", File = ".cursor/hooks.json", Location = "hooks.stop[0]", Message = "not executable" }],
            });

        public Task<ConventionsResponse> GetConventionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConventionsResponse
            {
                Available = true,
                IncludesUserScope = true,
                Commands = [],
                Skills = [],
                Rules = [],
                Subagents = [],
                HookFiles = [],
                McpFiles =
                [
                    new ConventionConfigFileInfo { Scope = ConventionScopeKind.Project, SourceDirectory = ".cursor", DirectoryRank = 2, Origin = ".cursor/mcp.json" },
                    new ConventionConfigFileInfo { Scope = ConventionScopeKind.User, SourceDirectory = ".cursor", DirectoryRank = 2, Origin = "~/.cursor/mcp.json" },
                ],
                Diagnostics =
                [
                    new ConventionDiagnosticInfo { Item = ConventionItemType.Config, Origin = ".cursor/mcp.json", Code = ConventionDiagnosticCode.Malformed, Reason = "not valid JSON" },
                    new ConventionDiagnosticInfo { Item = ConventionItemType.Skill, Origin = ".ompos/skills/x/SKILL.md", Code = ConventionDiagnosticCode.Invalid, Reason = "skill problem" },
                ],
            });

        private static WorkspaceHookInfo Hook(string id, string eventName, string? command, string[]? args, bool enabled) => new()
        {
            Id = id,
            PluginId = "cursor-hooks",
            Scope = "project",
            EventName = eventName,
            IsOmpEvent = false,
            Type = "command",
            Command = command,
            Args = args,
            Enabled = enabled,
            FailClosed = false,
            Blocking = false,
            PreConsented = false,
            TimeoutSeconds = 5,
            WorkingDirectory = "<workspace>",
            SourceFile = ".cursor/hooks.json",
        };
    }
}
