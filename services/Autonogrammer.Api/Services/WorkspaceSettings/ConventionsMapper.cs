using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Workspace.Conventions;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>Maps what convention discovery found to the wire shapes. Absolute paths never leave this class: only the origin the discovery already made relative does.</summary>
internal static class ConventionsMapper
{
    /// <summary>Maps a discovery result.</summary>
    /// <param name="set">What discovery found.</param>
    /// <param name="root">The workspace folder, removed from diagnostic sentences.</param>
    /// <param name="home">The home directory that was read, or null; removed from diagnostic sentences.</param>
    /// <returns>The response for an available workspace.</returns>
    public static ConventionsResponse ToResponse(ConventionSet set, string root, string? home)
    {
        ArgumentNullException.ThrowIfNull(set);
        return new ConventionsResponse
        {
            Available = true,
            IncludesUserScope = home is not null,
            Commands = [.. set.Commands.Select(ToCommand)],
            Skills = [.. set.Skills.Where(static skill => skill.Scope != ConventionScope.Builtin).Select(ToSkill)],
            Rules = [.. set.Rules.Select(ToRule)],
            Subagents = [.. set.Subagents.Select(ToSubagent)],
            HookFiles = [.. set.HookFiles.Select(ToFile)],
            McpFiles = [.. set.McpFiles.Select(ToFile)],
            Diagnostics = [.. set.Diagnostics.Where(static diagnostic => !diagnostic.Origin.StartsWith("builtin:", StringComparison.Ordinal)).Select(diagnostic => ToDiagnostic(diagnostic, root, home))],
        };
    }

    /// <summary>Builds the answer for a workspace that cannot be read.</summary>
    /// <param name="reason">The sentence that says why.</param>
    /// <param name="includesUserScope">Whether user scope is switched on.</param>
    /// <returns>The response with empty lists.</returns>
    public static ConventionsResponse Unavailable(string reason, bool includesUserScope) => new()
    {
        Available = false,
        Reason = reason,
        IncludesUserScope = includesUserScope,
        Commands = [],
        Skills = [],
        Rules = [],
        Subagents = [],
        HookFiles = [],
        McpFiles = [],
        Diagnostics = [],
    };

    private static ConventionCommandInfo ToCommand(ConventionCommand command) => new()
    {
        Name = command.Name,
        Description = command.Description,
        ArgumentHint = command.ArgumentHint,
        Template = command.Template,
        Scope = ToScope(command.Scope),
        SourceDirectory = command.SourceDirectory,
        DirectoryRank = Rank(command.SourceDirectory),
        Origin = command.Origin,
    };

    private static ConventionSkillInfo ToSkill(ConventionSkill skill) => new()
    {
        Name = skill.Name,
        Description = skill.Description,
        Paths = skill.Paths,
        DisableModelInvocation = skill.DisableModelInvocation,
        UserInvocable = skill.UserInvocable,
        ScopeDirectory = skill.ScopeDirectory,
        Scope = ToScope(skill.Scope),
        SourceDirectory = skill.SourceDirectory,
        DirectoryRank = Rank(skill.SourceDirectory),
        Origin = skill.Origin,
    };

    private static ConventionRuleInfo ToRule(ConventionRule rule) => new()
    {
        Name = rule.Name,
        Type = ToType(rule.Kind),
        Description = rule.Description,
        Globs = rule.Globs,
        AlwaysApply = rule.AlwaysApply,
        ScopeDirectory = rule.ScopeDirectory,
        Scope = ToScope(rule.Scope),
        SourceDirectory = rule.SourceDirectory,
        DirectoryRank = Rank(rule.SourceDirectory),
        Origin = rule.Origin,
    };

    private static ConventionSubagentInfo ToSubagent(ConventionSubagent agent) => new()
    {
        Name = agent.Name,
        Description = agent.Description,
        Model = agent.Model,
        ReadOnly = agent.ReadOnly,
        IsBackground = agent.IsBackground,
        Instructions = agent.Instructions,
        Scope = ToScope(agent.Scope),
        SourceDirectory = agent.SourceDirectory,
        DirectoryRank = Rank(agent.SourceDirectory),
        Origin = agent.Origin,
    };

    private static ConventionDiagnosticInfo ToDiagnostic(ConventionDiagnostic diagnostic, string root, string? home) => new()
    {
        Item = ToItem(diagnostic.Item),
        Origin = diagnostic.Origin,
        Code = ToCode(diagnostic.Kind),
        Reason = PathRedactor.Redact(diagnostic.Reason, root, home),
    };

    private static ConventionConfigFileInfo ToFile(ConventionConfigFile file) => new()
    {
        Scope = ToScope(file.Scope),
        SourceDirectory = file.SourceDirectory,
        DirectoryRank = Rank(file.SourceDirectory),
        Origin = file.Origin,
    };

    private static int? Rank(string sourceDirectory)
    {
        for (var index = 0; index < ConventionDirectories.Names.Count; index++)
        {
            if (string.Equals(ConventionDirectories.Names[index], sourceDirectory, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return null;
    }

    private static ConventionScopeKind ToScope(ConventionScope scope) =>
        scope == ConventionScope.User ? ConventionScopeKind.User : ConventionScopeKind.Project;

    private static ConventionItemType ToItem(ConventionItemKind item) => item switch
    {
        ConventionItemKind.Command => ConventionItemType.Command,
        ConventionItemKind.Skill => ConventionItemType.Skill,
        ConventionItemKind.Rule => ConventionItemType.Rule,
        ConventionItemKind.Subagent => ConventionItemType.Subagent,
        _ => ConventionItemType.Config,
    };

    private static ConventionDiagnosticCode ToCode(ConventionDiagnosticKind kind) => kind switch
    {
        ConventionDiagnosticKind.Malformed => ConventionDiagnosticCode.Malformed,
        ConventionDiagnosticKind.TooLarge => ConventionDiagnosticCode.TooLarge,
        ConventionDiagnosticKind.Unreadable => ConventionDiagnosticCode.Unreadable,
        ConventionDiagnosticKind.EscapesRoot => ConventionDiagnosticCode.EscapesRoot,
        ConventionDiagnosticKind.Duplicate => ConventionDiagnosticCode.Duplicate,
        _ => ConventionDiagnosticCode.Invalid,
    };

    private static ConventionRuleType ToType(ConventionRuleKind kind) => kind switch
    {
        ConventionRuleKind.Mdc => ConventionRuleType.Mdc,
        ConventionRuleKind.Markdown => ConventionRuleType.Markdown,
        ConventionRuleKind.AgentsFile => ConventionRuleType.AgentsFile,
        ConventionRuleKind.ClaudeFile => ConventionRuleType.ClaudeFile,
        _ => ConventionRuleType.CursorRules,
    };
}
