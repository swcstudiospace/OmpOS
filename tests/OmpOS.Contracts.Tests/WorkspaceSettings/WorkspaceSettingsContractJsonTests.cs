using System.Text.Json;
using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.WorkspaceSettings;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.WorkspaceSettings;

/// <summary>Verifies the workspace settings shapes: wire names, enum words, omitted nulls, and a byte-stable round trip through <see cref="WorkspaceSettingsJsonContext"/>.</summary>
public sealed class WorkspaceSettingsContractJsonTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A conventions answer round-trips, writes enum words and kebab names, and omits null members.</summary>
    [Fact]
    public void Conventions_RoundTrip_WithWireNames()
    {
        var response = new ConventionsResponse
        {
            Available = true,
            IncludesUserScope = true,
            Commands =
            [
                new ConventionCommandInfo
                {
                    Name = "ship",
                    Description = "Ship it",
                    Template = "Deploy $ARGUMENTS",
                    Scope = ConventionScopeKind.User,
                    SourceDirectory = ".cursor",
                    DirectoryRank = 2,
                    Origin = "~/.cursor/commands/ship.md",
                },
            ],
            Skills = [],
            Rules =
            [
                new ConventionRuleInfo
                {
                    Name = "AGENTS.md",
                    Type = ConventionRuleType.AgentsFile,
                    Description = "Be brief",
                    Globs = [],
                    AlwaysApply = true,
                    ScopeDirectory = string.Empty,
                    Scope = ConventionScopeKind.Project,
                    SourceDirectory = string.Empty,
                    Origin = "AGENTS.md",
                },
            ],
            Subagents = [],
            HookFiles = [],
            McpFiles = [],
            Diagnostics =
            [
                new ConventionDiagnosticInfo { Item = ConventionItemType.Command, Origin = ".cursor/commands/x.md", Code = ConventionDiagnosticCode.TooLarge, Reason = "too big" },
            ],
        };

        var json = JsonSerializer.Serialize(response, WorkspaceSettingsJsonContext.Default.ConventionsResponse);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.TryGetProperty("reason", out _).ShouldBeFalse();
        root.GetProperty("commands")[0].GetProperty("scope").GetString().ShouldBe("user");
        root.GetProperty("commands")[0].GetProperty("directoryRank").GetInt32().ShouldBe(2);
        root.GetProperty("commands")[0].TryGetProperty("argumentHint", out _).ShouldBeFalse();
        root.GetProperty("rules")[0].GetProperty("type").GetString().ShouldBe("agents-file");
        root.GetProperty("rules")[0].TryGetProperty("directoryRank", out _).ShouldBeFalse();
        root.GetProperty("diagnostics")[0].GetProperty("code").GetString().ShouldBe("too-large");
        root.GetProperty("diagnostics")[0].GetProperty("item").GetString().ShouldBe("command");
        var back = JsonSerializer.Deserialize(json, WorkspaceSettingsJsonContext.Default.ConventionsResponse).ShouldNotBeNull();
        back.Commands.ShouldHaveSingleItem().Scope.ShouldBe(ConventionScopeKind.User);
        back.Rules.ShouldHaveSingleItem().Type.ShouldBe(ConventionRuleType.AgentsFile);
        back.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(ConventionDiagnosticCode.TooLarge);
        JsonSerializer.Serialize(back, WorkspaceSettingsJsonContext.Default.ConventionsResponse).ShouldBe(json);
    }

    /// <summary>The runtime answer has exactly its documented members, in camel case.</summary>
    [Fact]
    public void Runtime_HasExactlyTheDocumentedMembers()
    {
        var response = new RuntimeSettingsResponse
        {
            HarnessAdapter = "Acp",
            SessionStore = "Postgres",
            CoordinationStore = "Dragonfly",
            RunLeaseSeconds = 30,
            RunLeaseRenewSeconds = 10,
            MaxPromptLength = 64000,
            MaxRequestBodyBytes = 1_048_576,
            PromptsPerMinute = 60,
            SessionsPerMinute = 60,
            WorkspaceRequestsPerMinute = 120,
            WorkspaceConfigured = true,
            IncludesUserScope = false,
            MaxWorkspaceFileBytes = 1_048_576,
        };

        var json = JsonSerializer.Serialize(response, WorkspaceSettingsJsonContext.Default.RuntimeSettingsResponse);

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(static property => property.Name).ShouldBe(
            [
                "harnessAdapter", "sessionStore", "coordinationStore", "runLeaseSeconds", "runLeaseRenewSeconds", "maxPromptLength", "maxRequestBodyBytes",
                "promptsPerMinute", "sessionsPerMinute", "workspaceRequestsPerMinute", "workspaceConfigured", "includesUserScope", "maxWorkspaceFileBytes",
            ]);
        JsonSerializer.Deserialize(json, WorkspaceSettingsJsonContext.Default.RuntimeSettingsResponse).ShouldBe(response);
    }

    /// <summary>The plugins answer carries the shared plugin check with its lower-case status word.</summary>
    [Fact]
    public void Plugins_CarryTheSharedPluginCheck()
    {
        var response = new PluginsSettingsResponse
        {
            Check = new PluginCheckResult { Status = PluginCheckStatus.Degraded, Summary = "One missing." },
            Plugins = [new WorkspacePluginInfo { Id = "gsd", DisplayName = "GSD", Role = "Planning", Found = false }],
        };

        var json = JsonSerializer.Serialize(response, WorkspaceSettingsJsonContext.Default.PluginsSettingsResponse);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("check").GetProperty("status").GetString().ShouldBe("degraded");
        document.RootElement.GetProperty("plugins")[0].TryGetProperty("version", out _).ShouldBeFalse();
        var back = JsonSerializer.Deserialize(json, WorkspaceSettingsJsonContext.Default.PluginsSettingsResponse).ShouldNotBeNull();
        back.Check.Status.ShouldBe(PluginCheckStatus.Degraded);
        back.Plugins.ShouldHaveSingleItem().Found.ShouldBeFalse();
    }

    /// <summary>The hooks answer round-trips with its optional members left out when null.</summary>
    [Fact]
    public void Hooks_RoundTrip()
    {
        var response = new HooksResponse
        {
            Available = true,
            ConsentRecorded = false,
            Hooks =
            [
                new WorkspaceHookInfo
                {
                    Id = "cursor-project:abc:beforeShellExecution:0",
                    PluginId = "cursor-project:abc",
                    Scope = "project",
                    EventName = "beforeShellExecution",
                    IsOmpEvent = false,
                    Type = "command",
                    Command = "./audit.sh",
                    Enabled = true,
                    FailClosed = false,
                    Blocking = true,
                    PreConsented = false,
                    TimeoutSeconds = 30,
                    WorkingDirectory = ".",
                    SourceFile = ".cursor/hooks.json",
                },
            ],
            Sources = [new WorkspaceHookSourceInfo { Scope = "project", File = ".cursor/hooks.json", Format = "hooks-json", Priority = 1 }],
            Diagnostics = [new WorkspaceHookDiagnosticInfo { Severity = "warning", File = ".cursor/hooks.json", Location = "$", Message = "m" }],
        };

        var json = JsonSerializer.Serialize(response, WorkspaceSettingsJsonContext.Default.HooksResponse);

        using var document = JsonDocument.Parse(json);
        var hook = document.RootElement.GetProperty("hooks")[0];
        hook.TryGetProperty("args", out _).ShouldBeFalse();
        hook.TryGetProperty("matcher", out _).ShouldBeFalse();
        hook.GetProperty("timeoutSeconds").GetDouble().ShouldBe(30);
        var back = JsonSerializer.Deserialize(json, WorkspaceSettingsJsonContext.Default.HooksResponse).ShouldNotBeNull();
        JsonSerializer.Serialize(back, WorkspaceSettingsJsonContext.Default.HooksResponse).ShouldBe(json);
    }

    /// <summary>The automations answer keeps the last run and the pause only when present, with the timestamp in ISO form.</summary>
    [Fact]
    public void Automations_RoundTrip()
    {
        var response = new AutomationsResponse
        {
            Available = true,
            Automations =
            [
                new WorkspaceAutomationInfo
                {
                    Id = "nightly",
                    Trigger = "schedule",
                    Enabled = true,
                    MaxRuntimeSeconds = 900,
                    LastRun = new AutomationLastRunInfo { Outcome = "failed", Reason = "why", RecordedAt = Stamp },
                },
            ],
        };

        var json = JsonSerializer.Serialize(response, WorkspaceSettingsJsonContext.Default.AutomationsResponse);

        using var document = JsonDocument.Parse(json);
        var row = document.RootElement.GetProperty("automations")[0];
        row.GetProperty("lastRun").GetProperty("recordedAt").GetString().ShouldBe("2026-10-27T09:00:00+00:00");
        row.TryGetProperty("pause", out _).ShouldBeFalse();
        var back = JsonSerializer.Deserialize(json, WorkspaceSettingsJsonContext.Default.AutomationsResponse).ShouldNotBeNull();
        back.Automations.ShouldHaveSingleItem().LastRun.ShouldNotBeNull().RecordedAt.ShouldBe(Stamp);
    }

    /// <summary>The tree and file answers write the entry kind as a word and a folder without a size.</summary>
    [Fact]
    public void TreeAndFile_RoundTrip()
    {
        var tree = new WorkspaceTreeResponse
        {
            Path = "src",
            Entries = [new WorkspaceTreeEntry { Name = "a.cs", Kind = WorkspaceEntryKind.File, Size = 12 }, new WorkspaceTreeEntry { Name = "inner", Kind = WorkspaceEntryKind.Directory }],
            Truncated = false,
        };
        var file = new WorkspaceFileResponse { Path = "src/a.cs", Size = 12, Content = "class A {}\n" };

        var treeJson = JsonSerializer.Serialize(tree, WorkspaceSettingsJsonContext.Default.WorkspaceTreeResponse);
        var fileJson = JsonSerializer.Serialize(file, WorkspaceSettingsJsonContext.Default.WorkspaceFileResponse);

        using var treeDocument = JsonDocument.Parse(treeJson);
        var entries = treeDocument.RootElement.GetProperty("entries");
        entries[0].GetProperty("kind").GetString().ShouldBe("file");
        entries[0].GetProperty("size").GetInt64().ShouldBe(12);
        entries[1].GetProperty("kind").GetString().ShouldBe("directory");
        entries[1].TryGetProperty("size", out _).ShouldBeFalse();
        JsonSerializer.Deserialize(treeJson, WorkspaceSettingsJsonContext.Default.WorkspaceTreeResponse).ShouldNotBeNull().Entries.Count.ShouldBe(2);
        JsonSerializer.Deserialize(fileJson, WorkspaceSettingsJsonContext.Default.WorkspaceFileResponse).ShouldBe(file);
    }

    /// <summary>The network answer round-trips.</summary>
    [Fact]
    public void Network_RoundTrip()
    {
        var response = new NetworkSettingsResponse
        {
            Checked = false,
            Reason = "The API sends no request.",
            Endpoints = [new NetworkEndpointInfo { Name = "Sign-in authority", Host = "login.example.com:8443" }],
        };

        var json = JsonSerializer.Serialize(response, WorkspaceSettingsJsonContext.Default.NetworkSettingsResponse);

        json.ShouldBe("{\"checked\":false,\"reason\":\"The API sends no request.\",\"endpoints\":[{\"name\":\"Sign-in authority\",\"host\":\"login.example.com:8443\"}]}");
        JsonSerializer.Deserialize(json, WorkspaceSettingsJsonContext.Default.NetworkSettingsResponse).ShouldNotBeNull().Endpoints.ShouldHaveSingleItem().Host.ShouldBe("login.example.com:8443");
    }
}
