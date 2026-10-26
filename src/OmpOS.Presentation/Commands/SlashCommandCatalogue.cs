namespace OmpOS.Presentation.Commands;

/// <summary>The slash commands that OmpOS ships.</summary>
public static class SlashCommandCatalogue
{
    /// <summary>
    /// The shipped command catalogue. It is empty on purpose: a command name is added only with a verified reference. The
    /// candidate names in docs/plans/vision-research/cursor-slash-commands.md are research input, and none is verified as an
    /// OmpOS command yet, so none is registered here.
    /// </summary>
    public static SlashCommandRegistry Shipped { get; } = new([]);

    /// <summary>
    /// The Cursor reference that the candidate names were checked against: the research record and the date it was read.
    /// </summary>
    public static SlashCommandReference Reference { get; } = new(
        "docs/plans/vision-research/cursor-slash-commands.md",
        new DateOnly(2026, 10, 22));
}
