namespace Autonogrammer.Presentation.Commands;

/// <summary>What runs a slash command when it is sent.</summary>
public enum SlashHandlerKind
{
    /// <summary>The raw text goes to omp as the prompt, and omp runs the command (only for commands omp's ACP mode handles).</summary>
    OmpDelegated = 0,

    /// <summary>The head runs a named action through <see cref="ISlashActionHost"/>: open Settings, start a new chat, switch mode or model.</summary>
    NativeAction = 1,

    /// <summary>A discovered skill (<c>SKILL.md</c>) is run: the skill is handed to omp.</summary>
    Skill = 2,

    /// <summary>The task is handed to a cloud agent, through the <c>cloud.handoff</c> action of <see cref="ISlashActionHost"/>.</summary>
    CloudHandoff = 3,

    /// <summary>Autonogrammer cannot run the command. <see cref="SlashCommandDescriptor.DisabledReason"/> says why.</summary>
    Unavailable = 4,
}
