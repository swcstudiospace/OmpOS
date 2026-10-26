using System.Collections.Frozen;

namespace OmpOS.Presentation.Commands;

/// <summary>The slash commands that OmpOS ships.</summary>
public static class SlashCommandCatalogue
{
    /// <summary>
    /// The shipped commands, each with the row of docs/plans/vision-research/cursor-slash-commands.md that it cites. A command is
    /// added only when OmpOS has a real handler for it; the other documented names stay research input and are not registered.
    /// </summary>
    private static readonly (SlashCommandDescriptor Command, string ResearchRow)[] Entries =
    [
        (
            new SlashCommandDescriptor
            {
                Name = "side",
                Description = "Opens a side chat with its own transcript. The parent thread is left untouched.",
                Platforms = CommandPlatforms.Linux | CommandPlatforms.Windows | CommandPlatforms.MacOS
                    | CommandPlatforms.Ios | CommandPlatforms.Android | CommandPlatforms.Web,
                HandlerId = "chats.side.open",
            },
            "B01"),
    ];

    /// <summary>
    /// The shipped command catalogue. Every command has a real handler: <c>side</c> opens a chat through
    /// <see cref="Chats.SideChatPanel.Open"/>. Any other name in docs/plans/vision-research/cursor-slash-commands.md is research
    /// input until a handler exists, and is not registered here.
    /// </summary>
    public static SlashCommandRegistry Shipped { get; } = new(Entries.Select(static entry => entry.Command));

    /// <summary>
    /// The Cursor reference that the candidate names were checked against: the research record and the date it was read.
    /// </summary>
    public static SlashCommandReference Reference { get; } = new(
        "docs/plans/vision-research/cursor-slash-commands.md",
        new DateOnly(2026, 10, 22));

    /// <summary>
    /// The row identifier of <see cref="SlashCommandReference.Source"/> that each shipped command cites, keyed by command name
    /// (ordinal). The row holds the Cursor syntax the command was verified against.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ResearchRows { get; } =
        Entries.ToFrozenDictionary(static entry => entry.Command.Name, static entry => entry.ResearchRow, StringComparer.Ordinal);
}
