using System.Globalization;

namespace OmpOS.Presentation.Commands;

/// <summary>Every user-facing sentence of the slash palette. The view and its tests read the same constants, so a copy change is made here and nowhere else.</summary>
public static class SlashPaletteCopy
{
    /// <summary>The tag of a command the omp engine advertised.</summary>
    public const string SourceOmp = "omp";

    /// <summary>The tag of a command of the OmpOS catalogue.</summary>
    public const string SourceOmpOs = "OmpOS";

    /// <summary>The accessible name of the list of commands.</summary>
    public const string ListName = "Slash commands";

    /// <summary>The help text of the list: how to move through it and what each key does.</summary>
    public const string ListHelp = "Up and Down choose a command. Enter or Tab puts it in the message. Escape closes the list. Nothing runs until the message is sent.";

    /// <summary>The accessible name of the live region that announces how many commands match.</summary>
    public const string CountName = "Slash command results";

    /// <summary>The accessible name of the live region that says why no command is listed, or why the highlighted command cannot be chosen.</summary>
    public const string MessageName = "Slash command message";

    /// <summary>The announcement when no command matches the typed name.</summary>
    public const string NoCommands = "No commands";

    /// <summary>Builds the announcement of how many commands match the typed name.</summary>
    /// <param name="count">The number of rows the palette lists.</param>
    /// <returns>The sentence the count line and the live region carry.</returns>
    public static string ResultCount(int count) => count switch
    {
        <= 0 => NoCommands,
        1 => "1 command",
        _ => string.Create(CultureInfo.InvariantCulture, $"{count} commands"),
    };

    /// <summary>Builds the accessible name of one row: the command, its input hint, what it does and where it comes from.</summary>
    /// <param name="entry">The row.</param>
    /// <returns>The name a screen reader speaks for the row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public static string EntryName(SlashPaletteEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var parts = new List<string> { entry.DisplayName };
        if (entry.HasInputHint)
        {
            parts.Add(entry.InputHint!);
        }

        if (entry.HasDescription)
        {
            parts.Add(entry.Description);
        }

        parts.Add(entry.SourceLabel);
        return string.Join(", ", parts);
    }
}
