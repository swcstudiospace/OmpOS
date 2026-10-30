using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Web.Services.Settings;

namespace Autonogrammer.Web.Tests.Settings;

/// <summary>Builders shared by the workspace settings source tests: a client over a scripted handler, JSON responses and sample answers of the API.</summary>
internal static class WorkspaceFixtures
{
    /// <summary>Creates an HTTP client whose base address is the API origin.</summary>
    /// <param name="handler">The scripted handler.</param>
    /// <returns>The client. The caller disposes it.</returns>
    public static HttpClient Http(ScriptedHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };

    /// <summary>Creates a JSON response from a contract value.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    /// <param name="status">The status.</param>
    /// <param name="value">The value.</param>
    /// <param name="typeInfo">The source-generated metadata.</param>
    /// <returns>The response. The caller disposes it.</returns>
    public static HttpResponseMessage Json<T>(HttpStatusCode status, T value, JsonTypeInfo<T> typeInfo) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(value, typeInfo), Encoding.UTF8, "application/json") };

    /// <summary>Creates a response with a raw text body.</summary>
    /// <param name="status">The status.</param>
    /// <param name="body">The body text.</param>
    /// <returns>The response. The caller disposes it.</returns>
    public static HttpResponseMessage Text(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Builds a conventions answer with the given parts and every other list empty.</summary>
    /// <param name="commands">The commands.</param>
    /// <param name="skills">The skills.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="subagents">The subagents.</param>
    /// <param name="diagnostics">The diagnostics.</param>
    /// <returns>The answer, available.</returns>
    public static ConventionsResponse Conventions(
        IReadOnlyList<ConventionCommandInfo>? commands = null,
        IReadOnlyList<ConventionSkillInfo>? skills = null,
        IReadOnlyList<ConventionRuleInfo>? rules = null,
        IReadOnlyList<ConventionSubagentInfo>? subagents = null,
        IReadOnlyList<ConventionDiagnosticInfo>? diagnostics = null) => new()
        {
            Available = true,
            IncludesUserScope = false,
            Commands = commands ?? [],
            Skills = skills ?? [],
            Rules = rules ?? [],
            Subagents = subagents ?? [],
            HookFiles = [],
            McpFiles = [],
            Diagnostics = diagnostics ?? [],
        };

    /// <summary>Builds a command.</summary>
    /// <param name="name">The name.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="origin">The origin.</param>
    /// <returns>The command.</returns>
    public static ConventionCommandInfo Command(string name, ConventionScopeKind scope = ConventionScopeKind.Project, string origin = ".cursor/commands/x.md") => new()
    {
        Name = name,
        Description = "Does " + name,
        ArgumentHint = "<arg>",
        Template = "Run " + name + " $ARGUMENTS",
        Scope = scope,
        SourceDirectory = ".cursor",
        DirectoryRank = 2,
        Origin = origin,
    };

    /// <summary>Builds a diagnostic.</summary>
    /// <param name="item">The item type.</param>
    /// <param name="origin">The origin.</param>
    /// <param name="reason">The reason.</param>
    /// <returns>The diagnostic.</returns>
    public static ConventionDiagnosticInfo Diagnostic(ConventionItemType item, string origin, string reason) => new()
    {
        Item = item,
        Origin = origin,
        Code = ConventionDiagnosticCode.Malformed,
        Reason = reason,
    };

    /// <summary>Creates the client under test over a scripted handler.</summary>
    /// <param name="http">The HTTP client.</param>
    /// <returns>The settings client.</returns>
    public static WorkspaceSettingsClient Client(HttpClient http) => new(http);
}
