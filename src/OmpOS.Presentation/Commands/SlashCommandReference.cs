namespace OmpOS.Presentation.Commands;

/// <summary>
/// The reference that a command catalogue's names were verified against (VX-02). Cursor publishes no single version for its
/// slash commands, so the reference is the research record and the date it was read; the record gives a version per row.
/// </summary>
/// <param name="Source">The repository path of the research record that holds the verified rows.</param>
/// <param name="ReadOn">The date the reference was read.</param>
public sealed record SlashCommandReference(string Source, DateOnly ReadOn);
