namespace Autonogrammer.Presentation.Commands;

/// <summary>The text is a command that a head runs through <see cref="ISlashActionHost"/>.</summary>
/// <param name="Command">The catalogue command.</param>
/// <param name="Request">The action to run, with the fixed argument and the typed text.</param>
/// <param name="Alternatives">The other sources that share the name and were not chosen, each with its source label; empty when the name is unambiguous.</param>
public sealed record SlashRunAction(SlashCommandDescriptor Command, SlashActionRequest Request, IReadOnlyList<SlashCandidate> Alternatives) : SlashResolution;
