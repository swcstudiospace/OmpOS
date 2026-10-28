using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Settings.Customize;
using OmpOS.Workspace.Conventions;

namespace OmpOS.Native.Views.Conventions;

/// <summary>Maps what convention discovery found to what the slash palette and the Settings pages show.</summary>
public static class ConventionMapping
{
    /// <summary>Maps a discovery result to the commands, skills and subagents of the slash palette.</summary>
    /// <param name="set">What discovery found.</param>
    /// <returns>The palette's view of it. The problems are every diagnostic as one sentence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="set"/> is null.</exception>
    public static SlashDiscovery ToDiscovery(ConventionSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        return new SlashDiscovery(
            [.. set.Commands.Select(static command => new SlashDiscoveredCommand(command.Name, command.Description, command.ArgumentHint, command.Template, ToSlashScope(command.Scope), command.Origin))],
            [.. set.Skills.Select(static skill => new SlashDiscoveredSkill(skill.Name, skill.Description, skill.UserInvocable, skill.Path, ToSlashScope(skill.Scope), skill.Origin))],
            [.. set.Subagents.Select(static agent => new SlashDiscoveredSubagent(agent.Name, agent.Description, agent.Model, agent.ReadOnly, agent.IsBackground, agent.Instructions, ToSlashScope(agent.Scope), agent.Origin))],
            Problems(set, ConventionItemKind.Command, ConventionItemKind.Subagent, ConventionItemKind.Config));
    }

    /// <summary>Maps the rules and instruction files to the Rules page.</summary>
    /// <param name="set">What discovery found.</param>
    /// <returns>The entries, with the problems of rule files as the page's problems.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="set"/> is null.</exception>
    public static CustomizeCatalog ToRulesCatalog(ConventionSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        return new CustomizeCatalog(
            [.. set.Rules.Select(static rule => new CustomizeEntry(rule.Name, ToCustomizeScope(rule.Scope), rule.Origin, DescribeRule(rule)))],
            Problems(set, ConventionItemKind.Rule));
    }

    /// <summary>Maps the skills to the Skills page.</summary>
    /// <param name="set">What discovery found.</param>
    /// <returns>The entries, with the problems of skill files as the page's problems.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="set"/> is null.</exception>
    public static CustomizeCatalog ToSkillsCatalog(ConventionSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        return new CustomizeCatalog(
            [.. set.Skills.Select(static skill => new CustomizeEntry(skill.Name, ToCustomizeScope(skill.Scope), skill.Origin, DescribeSkill(skill)))],
            Problems(set, ConventionItemKind.Skill));
    }

    /// <summary>Lists the problems of the given kinds. A built-in skill that a project or user skill of the same name replaced is not a problem: replacing it is what the built-in scope is for.</summary>
    private static string[] Problems(ConventionSet set, params ConventionItemKind[] kinds) =>
        [.. set.Diagnostics.Where(diagnostic => kinds.Contains(diagnostic.Item) && !IsShadowedBuiltin(diagnostic)).Select(static diagnostic => diagnostic.Origin + ": " + diagnostic.Reason)];

    private static bool IsShadowedBuiltin(ConventionDiagnostic diagnostic) =>
        diagnostic.Kind == ConventionDiagnosticKind.Duplicate && diagnostic.Origin.StartsWith("builtin:", StringComparison.Ordinal);

    private static SlashScope ToSlashScope(ConventionScope scope) => scope switch
    {
        ConventionScope.User => SlashScope.User,
        ConventionScope.Builtin => SlashScope.BuiltIn,
        _ => SlashScope.Project,
    };

    private static CustomizeScope ToCustomizeScope(ConventionScope scope) => scope switch
    {
        ConventionScope.User => CustomizeScope.User,
        ConventionScope.Builtin => CustomizeScope.BuiltIn,
        _ => CustomizeScope.Project,
    };

    private static string DescribeRule(ConventionRule rule)
    {
        var parts = new List<string>();
        if (rule.Description.Length > 0)
        {
            parts.Add(rule.Description);
        }

        if (rule.AlwaysApply && rule.Kind is ConventionRuleKind.Mdc or ConventionRuleKind.Markdown)
        {
            parts.Add("Always applied.");
        }

        if (rule.Globs.Count > 0)
        {
            parts.Add("Applies to " + string.Join(", ", rule.Globs) + ".");
        }

        return string.Join(' ', parts);
    }

    private static string DescribeSkill(ConventionSkill skill)
    {
        var parts = new List<string>();
        if (skill.Description.Length > 0)
        {
            parts.Add(skill.Description);
        }

        if (!skill.UserInvocable)
        {
            parts.Add("Not offered after a slash.");
        }

        if (skill.DisableModelInvocation)
        {
            parts.Add("The model does not pick it by itself.");
        }

        return string.Join(' ', parts);
    }
}
