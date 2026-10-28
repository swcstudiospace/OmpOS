using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Customize;

namespace OmpOS.Web.Services.Settings;

/// <summary>Reads the conventions of the server workspace and maps them to what the slash palette and the Rules and Skills pages show. It mirrors the desktop mapping.</summary>
internal static class WorkspaceConventionMapping
{
    /// <summary>Reads the conventions.</summary>
    /// <param name="client">The settings client.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The conventions of an available workspace.</returns>
    /// <exception cref="SettingsSourceException">The call failed or the API has no readable workspace; the message says why.</exception>
    internal static async Task<ConventionsResponse> ReadAsync(WorkspaceSettingsClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ConventionsResponse response;
        try
        {
            response = await client.GetConventionsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (WorkspaceSettingsFailure.IsExpected(exception))
        {
            throw new SettingsSourceException(WorkspaceSettingsFailure.Describe(exception), exception);
        }

        return response.Available
            ? response
            : throw new SettingsSourceException(response.Reason ?? "The server has no workspace folder to read.");
    }

    /// <summary>Maps the commands, skills and subagents to the slash palette's discovery. A skill's path is its origin: the server's absolute path is never sent.</summary>
    /// <param name="response">The conventions.</param>
    /// <returns>The discovery. The problems are the diagnostics of command, subagent and configuration files.</returns>
    internal static SlashDiscovery ToDiscovery(ConventionsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new SlashDiscovery(
            [.. response.Commands.Select(static command => new SlashDiscoveredCommand(command.Name, command.Description, command.ArgumentHint, command.Template, ToSlashScope(command.Scope), command.Origin))],
            [.. response.Skills.Select(static skill => new SlashDiscoveredSkill(skill.Name, skill.Description, skill.UserInvocable, skill.Origin, ToSlashScope(skill.Scope), skill.Origin))],
            [.. response.Subagents.Select(static agent => new SlashDiscoveredSubagent(agent.Name, agent.Description, agent.Model, agent.ReadOnly, agent.IsBackground, agent.Instructions, ToSlashScope(agent.Scope), agent.Origin))],
            Problems(response, ConventionItemType.Command, ConventionItemType.Subagent, ConventionItemType.Config));
    }

    /// <summary>Maps the rules and instruction files to the Rules page.</summary>
    /// <param name="response">The conventions.</param>
    /// <returns>The entries, with the diagnostics of rule files as problems.</returns>
    internal static CustomizeCatalog ToRulesCatalog(ConventionsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new CustomizeCatalog(
            [.. response.Rules.Select(static rule => new CustomizeEntry(rule.Name, ToCustomizeScope(rule.Scope), rule.Origin, DescribeRule(rule)))],
            Problems(response, ConventionItemType.Rule));
    }

    /// <summary>Maps the skills to the Skills page.</summary>
    /// <param name="response">The conventions.</param>
    /// <returns>The entries, with the diagnostics of skill files as problems.</returns>
    internal static CustomizeCatalog ToSkillsCatalog(ConventionsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new CustomizeCatalog(
            [.. response.Skills.Select(static skill => new CustomizeEntry(skill.Name, ToCustomizeScope(skill.Scope), skill.Origin, DescribeSkill(skill)))],
            Problems(response, ConventionItemType.Skill));
    }

    private static string[] Problems(ConventionsResponse response, params ConventionItemType[] kinds) =>
        [.. response.Diagnostics.Where(diagnostic => kinds.Contains(diagnostic.Item)).Select(static diagnostic => diagnostic.Origin + ": " + diagnostic.Reason)];

    private static SlashScope ToSlashScope(ConventionScopeKind scope) => scope == ConventionScopeKind.User ? SlashScope.User : SlashScope.Project;

    private static CustomizeScope ToCustomizeScope(ConventionScopeKind scope) => scope == ConventionScopeKind.User ? CustomizeScope.User : CustomizeScope.Project;

    private static string DescribeRule(ConventionRuleInfo rule)
    {
        var parts = new List<string>();
        if (rule.Description.Length > 0)
        {
            parts.Add(rule.Description);
        }

        if (rule.AlwaysApply && rule.Type is ConventionRuleType.Mdc or ConventionRuleType.Markdown)
        {
            parts.Add("Always applied.");
        }

        if (rule.Globs.Count > 0)
        {
            parts.Add("Applies to " + string.Join(", ", rule.Globs) + ".");
        }

        return string.Join(' ', parts);
    }

    private static string DescribeSkill(ConventionSkillInfo skill)
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
