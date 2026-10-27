namespace OmpOS.Presentation.Commands;

/// <summary>Where a row of the slash palette comes from. The source is shown as a small tag on the row.</summary>
public enum SlashPaletteSource
{
    /// <summary>The omp engine advertised the command (a built-in command, a skill, a plugin or a custom command). omp runs it when the prompt is sent.</summary>
    Omp,

    /// <summary>The OmpOS catalogue (<see cref="SlashCommandCatalogue"/>) registered the command.</summary>
    OmpOs,
}
