using System.Text;

namespace OmpOS.Presentation.Commands;

/// <summary>Builds the prompt text for the commands that omp does not know: a skill or a subagent that discovery found. omp reads the file itself, so the prompt names the file and says what to do.</summary>
public static class SlashPromptComposer
{
    /// <summary>The prefix of the name omp gives a skill command.</summary>
    public const string SkillCommandPrefix = "skill:";

    /// <summary>Builds the prompt that runs a skill. When the engine advertises the skill as <c>skill:&lt;name&gt;</c> the prompt is that command; otherwise it tells the engine to read the skill file and follow it.</summary>
    /// <param name="skill">The discovered skill.</param>
    /// <param name="typedText">The text typed after the name.</param>
    /// <param name="engineRunsSkill">Whether the engine advertises <c>skill:&lt;name&gt;</c>.</param>
    /// <returns>The prompt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="skill"/> or <paramref name="typedText"/> is null.</exception>
    public static string ForSkill(SlashDiscoveredSkill skill, string typedText, bool engineRunsSkill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(typedText);

        var trimmed = typedText.Trim();
        if (engineRunsSkill)
        {
            return trimmed.Length == 0 ? "/" + SkillCommandPrefix + skill.Name : "/" + SkillCommandPrefix + skill.Name + " " + trimmed;
        }

        var prompt = "Use the skill \"" + skill.Name + "\". Read its instructions from " + skill.Path + " and follow them.";
        return trimmed.Length == 0 ? prompt : prompt + "\n\n" + trimmed;
    }

    /// <summary>Builds the prompt that hands a task to a subagent. omp's own agents come from <c>.omp/agents</c>, so the prompt carries the definition that discovery found.</summary>
    /// <param name="subagent">The discovered subagent.</param>
    /// <param name="typedText">The task typed after the name.</param>
    /// <returns>The prompt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subagent"/> or <paramref name="typedText"/> is null.</exception>
    public static string ForSubagent(SlashDiscoveredSubagent subagent, string typedText)
    {
        ArgumentNullException.ThrowIfNull(subagent);
        ArgumentNullException.ThrowIfNull(typedText);

        var prompt = new StringBuilder();
        prompt.Append("Hand the task below to a subagent named \"").Append(subagent.Name).Append("\" with the task tool. Its definition comes from ").Append(subagent.Origin).Append(":\n");
        if (subagent.Description.Length > 0)
        {
            prompt.Append("Description: ").Append(subagent.Description).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(subagent.Model) && !string.Equals(subagent.Model, "inherit", StringComparison.OrdinalIgnoreCase))
        {
            prompt.Append("Model: ").Append(subagent.Model).Append('\n');
        }

        if (subagent.ReadOnly)
        {
            prompt.Append("The subagent must not change files.\n");
        }

        if (subagent.IsBackground)
        {
            prompt.Append("Run the subagent in the background.\n");
        }

        prompt.Append("Instructions:\n").Append(subagent.Instructions.Trim()).Append("\n\nTask: ").Append(typedText.Trim());
        return prompt.ToString();
    }
}
