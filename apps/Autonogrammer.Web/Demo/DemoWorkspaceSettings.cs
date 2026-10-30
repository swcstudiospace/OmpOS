using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.WorkspaceSettings;

namespace Autonogrammer.Web.Demo;

/// <summary>
/// The read-only workspace settings of the Demo workspace <c>acme-shop</c>, in the shapes of the real routes: the conventions (two rules, three
/// skills, two commands, one subagent, the hook and MCP files and one problem of an MCP file), the hooks, the runtime values, the plugin check, the
/// network endpoints and the automations answer. Every value is a fixture; the runtime values are the real defaults of the API.
/// </summary>
internal static class DemoWorkspaceSettings
{
    /// <summary>Gets the conventions of the workspace.</summary>
    internal static ConventionsResponse Conventions { get; } = new()
    {
        Available = true,
        IncludesUserScope = true,
        Commands =
        [
            new ConventionCommandInfo
            {
                Name = "test-product",
                Description = "Run the product tests and summarise the failures.",
                ArgumentHint = "[filter]",
                Template = "Run `dotnet test --filter $ARGUMENTS` for the Products area and summarise every failure in two lines each.",
                Scope = ConventionScopeKind.Project,
                SourceDirectory = ".autonogrammer",
                DirectoryRank = 0,
                Origin = ".autonogrammer/commands/test-product.md",
            },
            new ConventionCommandInfo
            {
                Name = "changelog",
                Description = "Draft the changelog entry for a version.",
                ArgumentHint = "[version]",
                Template = "Draft the CHANGELOG.md entry for $ARGUMENTS from the commits since the last tag.",
                Scope = ConventionScopeKind.Project,
                SourceDirectory = ".cursor",
                DirectoryRank = 2,
                Origin = ".cursor/commands/changelog.md",
            },
        ],
        Skills =
        [
            new ConventionSkillInfo
            {
                Name = "review-pr",
                Description = "Review a pull request against the shop's API conventions.",
                Paths = [],
                DisableModelInvocation = false,
                UserInvocable = true,
                ScopeDirectory = ".",
                Scope = ConventionScopeKind.Project,
                SourceDirectory = ".autonogrammer",
                DirectoryRank = 0,
                Origin = ".autonogrammer/skills/review-pr/SKILL.md",
            },
            new ConventionSkillInfo
            {
                Name = "release-notes",
                Description = "Write customer-facing release notes from merged pull requests.",
                Paths = [],
                DisableModelInvocation = true,
                UserInvocable = true,
                ScopeDirectory = ".",
                Scope = ConventionScopeKind.Project,
                SourceDirectory = ".cursor",
                DirectoryRank = 2,
                Origin = ".cursor/skills/release-notes/SKILL.md",
            },
            new ConventionSkillInfo
            {
                Name = "explain-error",
                Description = "Explain a stack trace in plain words and name the likely cause.",
                Paths = [],
                DisableModelInvocation = false,
                UserInvocable = true,
                ScopeDirectory = "~",
                Scope = ConventionScopeKind.User,
                SourceDirectory = ".autonogrammer",
                DirectoryRank = 0,
                Origin = "~/.autonogrammer/skills/explain-error/SKILL.md",
            },
        ],
        Rules =
        [
            new ConventionRuleInfo
            {
                Name = "code-style",
                Type = ConventionRuleType.Mdc,
                Description = "C# style of the shop: file-scoped namespaces, no async void, nullable on.",
                Globs = ["src/**/*.cs"],
                AlwaysApply = false,
                ScopeDirectory = ".",
                Scope = ConventionScopeKind.Project,
                SourceDirectory = ".cursor",
                DirectoryRank = 2,
                Origin = ".cursor/rules/code-style.mdc",
            },
            new ConventionRuleInfo
            {
                Name = "AGENTS.md",
                Type = ConventionRuleType.AgentsFile,
                Description = "How to build, test and run the shop; the Products API must stay backwards compatible.",
                Globs = [],
                AlwaysApply = true,
                ScopeDirectory = ".",
                Scope = ConventionScopeKind.Project,
                SourceDirectory = string.Empty,
                Origin = "AGENTS.md",
            },
        ],
        Subagents =
        [
            new ConventionSubagentInfo
            {
                Name = "verifier",
                Description = "Checks that a finished change builds and its tests pass, and reports what it ran.",
                ReadOnly = true,
                IsBackground = false,
                Instructions = "Run the build and the tests of the touched projects. Report the commands and the result. Change nothing.",
                Scope = ConventionScopeKind.Project,
                SourceDirectory = ".autonogrammer",
                DirectoryRank = 0,
                Origin = ".autonogrammer/agents/verifier.md",
            },
        ],
        HookFiles =
        [
            new ConventionConfigFileInfo { Scope = ConventionScopeKind.Project, SourceDirectory = ".cursor", DirectoryRank = 2, Origin = ".cursor/hooks.json" },
        ],
        McpFiles =
        [
            new ConventionConfigFileInfo { Scope = ConventionScopeKind.Project, SourceDirectory = ".cursor", DirectoryRank = 2, Origin = ".cursor/mcp.json" },
            new ConventionConfigFileInfo { Scope = ConventionScopeKind.Project, SourceDirectory = string.Empty, Origin = ".mcp.json" },
        ],
        Diagnostics =
        [
            new ConventionDiagnosticInfo
            {
                Item = ConventionItemType.Config,
                Origin = ".mcp.json",
                Code = ConventionDiagnosticCode.Malformed,
                Reason = "The file is not valid JSON: expected ',' after the \"inventory\" server (line 9).",
            },
        ],
    };

    /// <summary>Gets the hooks of the workspace.</summary>
    internal static HooksResponse Hooks { get; } = new()
    {
        Available = true,
        ConsentRecorded = false,
        Hooks =
        [
            Hook("cursor-hooks:beforeShellExecution:0", "beforeShellExecution", "./.cursor/hooks/audit-shell.sh", null, "Log every shell command before it runs.", blocking: true, failClosed: true, timeout: 10),
            Hook("cursor-hooks:afterFileEdit:0", "afterFileEdit", null, ["npx", "prettier", "--write", "."], "Format the files an agent edited.", blocking: false, failClosed: false, timeout: 30, matcher: "\\.(cs|ts|json)$"),
            Hook("cursor-hooks:stop:0", "stop", "./.cursor/hooks/notify-done.sh", null, "Post a note in the team channel when a run ends.", blocking: false, failClosed: false, timeout: 5, enabled: false),
        ],
        Sources =
        [
            new WorkspaceHookSourceInfo { Scope = "project", File = ".cursor/hooks.json", Format = "cursor", Priority = 0 },
        ],
        Diagnostics =
        [
            new WorkspaceHookDiagnosticInfo
            {
                Severity = "warning",
                File = ".cursor/hooks.json",
                Location = "hooks.stop[0]",
                Message = "The command ./.cursor/hooks/notify-done.sh is not executable; the hook is disabled until it is.",
            },
        ],
    };

    /// <summary>Gets the runtime values: the API defaults, with the stores named as the Demo.</summary>
    internal static RuntimeSettingsResponse Runtime { get; } = new()
    {
        HarnessAdapter = "demo (scripted, in this browser)",
        SessionStore = "demo (in this browser)",
        CoordinationStore = "demo (in this browser)",
        RunLeaseSeconds = 30,
        RunLeaseRenewSeconds = 10,
        MaxPromptLength = 64000,
        MaxRequestBodyBytes = 1048576,
        PromptsPerMinute = 60,
        SessionsPerMinute = 60,
        WorkspaceRequestsPerMinute = 120,
        WorkspaceConfigured = true,
        IncludesUserScope = true,
        MaxWorkspaceFileBytes = 1024 * 1024,
    };

    /// <summary>Gets the plugin check and the four connected plugins.</summary>
    internal static PluginsSettingsResponse Plugins { get; } = new()
    {
        Check = new PluginCheckResult { Status = PluginCheckStatus.Ok, Summary = "Ultrathink, Swarm, Substrate and GSD-Core are connected (Demo)." },
        Plugins =
        [
            Plugin("claude-ultrathink", "Claude Ultrathink", "Rewrites a prompt into a specification, a graph of thought and workflow waves.", "Pinned submodule plugins/claude-ultrathink."),
            Plugin("agent-swarm", "Agent Swarm", "Runs subagents after the planner.", "Pinned submodule plugins/agent-swarm."),
            Plugin("agent-substrate", "Agent Substrate", "Shares memory, leases, handoffs and the event ledger across sessions.", "Pinned submodule plugins/agent-substrate."),
            Plugin("gsd-core-omp", "GSD Core", "Drives the phases and plans a session works through.", "Tracked by Autonogrammer at plugins/gsd-core-omp."),
        ],
    };

    /// <summary>Gets the network endpoints the service talks to. No call is made in the Demo.</summary>
    internal static NetworkSettingsResponse Network { get; } = new()
    {
        Checked = false,
        Reason = "The Demo makes no network request: it answers from this browser, so no endpoint was checked.",
        Endpoints =
        [
            new NetworkEndpointInfo { Name = "Sign-in authority", Host = "not configured (Demo)" },
            new NetworkEndpointInfo { Name = "Session store", Host = "in this browser" },
            new NetworkEndpointInfo { Name = "Coordination store", Host = "in this browser" },
        ],
    };

    /// <summary>Gets the automations answer: the Demo has no automation engine.</summary>
    internal static AutomationsResponse Automations { get; } = new()
    {
        Available = false,
        Reason = "No automation engine is configured in the Demo.",
        Automations = [],
    };

    private static WorkspaceHookInfo Hook(
        string id,
        string eventName,
        string? command,
        string[]? args,
        string description,
        bool blocking,
        bool failClosed,
        double timeout,
        string? matcher = null,
        bool enabled = true) => new()
        {
            Id = id,
            PluginId = "cursor-hooks",
            Scope = "project",
            EventName = eventName,
            IsOmpEvent = false,
            Type = "command",
            Command = command,
            Args = args,
            Matcher = matcher,
            Description = description,
            Enabled = enabled,
            FailClosed = failClosed,
            Blocking = blocking,
            PreConsented = false,
            TimeoutSeconds = timeout,
            WorkingDirectory = "<workspace>",
            SourceFile = ".cursor/hooks.json",
        };

    private static WorkspacePluginInfo Plugin(string id, string name, string role, string detail) => new()
    {
        Id = id,
        DisplayName = name,
        Role = role,
        Version = "0.15.0",
        Found = true,
        Detail = detail,
    };
}
