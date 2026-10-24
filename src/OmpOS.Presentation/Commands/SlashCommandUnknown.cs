namespace OmpOS.Presentation.Commands;

/// <summary>The name is not registered.</summary>
/// <param name="Name">The name that was asked for.</param>
/// <param name="Suggestions">The closest registered names, nearest first. It is empty when nothing is close.</param>
public sealed record SlashCommandUnknown(string Name, IReadOnlyList<string> Suggestions) : SlashCommandResolution;
