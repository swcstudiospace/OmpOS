namespace Autonogrammer.Presentation.Commands;

/// <summary>
/// Decides what typed composer text means. Precedence for a name that several sources share: an exact catalogue token, an engine command (from omp's <c>available_commands_update</c>), a discovered
/// custom command, a skill, a subagent. The first source that can run it is chosen and the others are returned as alternatives with their source labels. A catalogue token Autonogrammer cannot run does not hide a
/// lower source that can; when nothing can run the name the resolution is a refusal with the reason. A name that no source knows passes through unchanged, so omp sees exactly what the user typed. The prefix
/// <c>&amp;</c> followed by white space hands the rest to a cloud agent.
/// </summary>
public sealed class SlashCommandResolver
{
    private const string CloudCommandName = "in-cloud";

    private readonly SlashCommandRegistry _catalogue;

    /// <summary>Initializes a new instance of the <see cref="SlashCommandResolver"/> class.</summary>
    /// <param name="catalogue">The Autonogrammer catalogue, normally <see cref="SlashCommandCatalogue.Shipped"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalogue"/> is null.</exception>
    public SlashCommandResolver(SlashCommandRegistry catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        _catalogue = catalogue;
    }

    /// <summary>Resolves typed composer text.</summary>
    /// <param name="input">The text as typed.</param>
    /// <param name="context">The engine commands, the discovered conventions and the action host.</param>
    /// <returns>The resolution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> or <paramref name="context"/> is null.</exception>
    public SlashResolution Resolve(string input, SlashResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        if (input.Length > 1 && input[0] == '&' && char.IsWhiteSpace(input[1]) && input[1..].Trim() is { Length: > 0 } task)
        {
            return ResolveCloudPrefix(task, context);
        }

        if (!TrySplit(input, out var name, out var typed))
        {
            return new SlashPassThrough(input);
        }

        var matches = new List<Match>();
        var refusal = CollectCatalogue(name, typed, input, context, matches);
        CollectEngine(name, input, context, matches);
        CollectDiscovered(name, typed, context, matches);

        if (matches.Count == 0)
        {
            return refusal is null ? new SlashPassThrough(input) : new SlashRefused(name, refusal);
        }

        var chosen = matches[0];
        var alternatives = matches.Skip(1).Select(static match => match.Candidate).ToArray();
        return chosen.Action is { } action
            ? new SlashRunAction(action, new SlashActionRequest(action.HandlerId, action.ActionArgument ?? string.Empty, typed), alternatives)
            : new SlashSendPrompt(chosen.Prompt!, chosen.Candidate, alternatives);
    }

    /// <summary>Splits <c>/name rest</c> at the first white space. A name that is empty or holds a slash is a path, not a command.</summary>
    private static bool TrySplit(string input, out string name, out string typed)
    {
        name = string.Empty;
        typed = string.Empty;
        if (input.Length < 2 || input[0] != '/')
        {
            return false;
        }

        var end = 1;
        while (end < input.Length && !char.IsWhiteSpace(input[end]))
        {
            end++;
        }

        name = input[1..end];
        typed = input[end..].Trim();
        return name.Length > 0 && !name.Contains('/', StringComparison.Ordinal);
    }

    private static string DelegatedPrompt(SlashCommandDescriptor command, string originalInput, string typed) =>
        command.EngineCommand is null ? originalInput : typed.Length == 0 ? "/" + command.EngineCommand : "/" + command.EngineCommand + " " + typed;

    private SlashResolution ResolveCloudPrefix(string task, SlashResolutionContext context)
    {
        if (_catalogue.Resolve(CloudCommandName) is not (SlashCommandAvailable or SlashCommandDisabled))
        {
            return new SlashPassThrough("& " + task);
        }

        var command = _catalogue.ListAll().Single(descriptor => string.Equals(descriptor.Name, CloudCommandName, StringComparison.Ordinal));
        if (command.DisabledReason is { } reason)
        {
            return new SlashRefused("&", reason);
        }

        return context.Host?.CanRun(SlashActionIds.CloudHandoff) == true
            ? new SlashRunAction(command, new SlashActionRequest(SlashActionIds.CloudHandoff, string.Empty, task), [])
            : new SlashRefused("&", SlashPaletteCopy.ActionNotInThisWindow);
    }

    /// <summary>Adds the catalogue candidate for a name. Returns the reason the catalogue refuses the name, or null when it does not.</summary>
    private string? CollectCatalogue(string name, string typed, string input, SlashResolutionContext context, List<Match> matches)
    {
        var command = _catalogue.ListAll().FirstOrDefault(descriptor => string.Equals(descriptor.Name, name, StringComparison.Ordinal));
        if (command is null || command.Kind == SlashHandlerKind.Skill)
        {
            return command is null ? null : SlashPaletteCopy.NoSkillInstalled(name);
        }

        if (command.DisabledReason is { } reason)
        {
            return reason;
        }

        var candidate = new SlashCandidate(name, SlashPaletteSource.Autonogrammer, command.Kind, SlashScope.BuiltIn, null);
        switch (command.Kind)
        {
            case SlashHandlerKind.OmpDelegated:
                if (!context.EngineCommands.Any(engine => string.Equals(engine.Name, name, StringComparison.Ordinal)))
                {
                    matches.Add(new Match(candidate, null, DelegatedPrompt(command, input, typed)));
                }

                return null;
            case SlashHandlerKind.NativeAction or SlashHandlerKind.CloudHandoff:
                if (context.Host?.CanRun(command.HandlerId) == true)
                {
                    matches.Add(new Match(candidate, command, null));
                    return null;
                }

                return SlashPaletteCopy.ActionNotInThisWindow;
            default:
                return command.DisabledReason;
        }
    }

    private static void CollectEngine(string name, string input, SlashResolutionContext context, List<Match> matches)
    {
        if (context.EngineCommands.Any(engine => string.Equals(engine.Name, name, StringComparison.Ordinal)))
        {
            matches.Add(new Match(new SlashCandidate(name, SlashPaletteSource.Engine, SlashHandlerKind.OmpDelegated, SlashScope.BuiltIn, null), null, input));
        }
    }

    private static void CollectDiscovered(string name, string typed, SlashResolutionContext context, List<Match> matches)
    {
        foreach (var command in context.Discovery.Commands.Where(command => string.Equals(command.Name, name, StringComparison.Ordinal)))
        {
            matches.Add(new Match(
                new SlashCandidate(name, SlashPaletteSource.Command, SlashHandlerKind.OmpDelegated, command.Scope, command.Origin),
                null,
                SlashCommandArguments.Expand(command.Template, typed)));
        }

        var engineSkill = context.EngineCommands.Any(engine => string.Equals(engine.Name, SlashPromptComposer.SkillCommandPrefix + name, StringComparison.Ordinal));
        var skill = context.Discovery.Skills.FirstOrDefault(found => found.UserInvocable && string.Equals(found.Name, name, StringComparison.Ordinal));
        if (skill is not null)
        {
            matches.Add(new Match(
                new SlashCandidate(name, SlashPaletteSource.Skill, SlashHandlerKind.Skill, skill.Scope, skill.Origin),
                null,
                SlashPromptComposer.ForSkill(skill, typed, engineSkill)));
        }
        else if (engineSkill)
        {
            matches.Add(new Match(
                new SlashCandidate(name, SlashPaletteSource.Engine, SlashHandlerKind.Skill, SlashScope.BuiltIn, null),
                null,
                SlashPromptComposer.ForSkill(new SlashDiscoveredSkill(name, string.Empty, true, string.Empty, SlashScope.BuiltIn, string.Empty), typed, engineRunsSkill: true)));
        }

        foreach (var subagent in context.Discovery.Subagents.Where(found => string.Equals(found.Name, name, StringComparison.Ordinal)))
        {
            matches.Add(new Match(
                new SlashCandidate(name, SlashPaletteSource.Subagent, SlashHandlerKind.OmpDelegated, subagent.Scope, subagent.Origin),
                null,
                SlashPromptComposer.ForSubagent(subagent, typed)));
        }
    }

    /// <summary>One source that can run the name: the action to run, or the prompt to send.</summary>
    private sealed record Match(SlashCandidate Candidate, SlashCommandDescriptor? Action, string? Prompt);
}
