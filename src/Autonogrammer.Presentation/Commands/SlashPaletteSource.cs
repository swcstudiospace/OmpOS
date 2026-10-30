namespace Autonogrammer.Presentation.Commands;

/// <summary>Where a row of the slash palette comes from. The source is shown as a small badge on the row.</summary>
public enum SlashPaletteSource
{
    /// <summary>The omp engine advertised the command (a built-in command, a skill, a plugin or a custom command). omp runs it when the prompt is sent.</summary>
    Engine = 0,

    /// <summary>The Autonogrammer catalogue (<see cref="SlashCommandCatalogue"/>) registered a command that Autonogrammer runs or forwards (built, delegated or partial).</summary>
    Autonogrammer = 1,

    /// <summary>The Autonogrammer catalogue lists a Cursor command that Autonogrammer cannot run. The row is disabled and says why.</summary>
    Cursor = 2,

    /// <summary>A custom command file (<c>commands/*.md</c>) that discovery found.</summary>
    Command = 3,

    /// <summary>A skill (<c>skills/&lt;name&gt;/SKILL.md</c>) that discovery found.</summary>
    Skill = 4,

    /// <summary>A subagent definition (<c>agents/*.md</c>) that discovery found.</summary>
    Subagent = 5,
}
