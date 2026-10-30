namespace Autonogrammer.Presentation.Commands;

/// <summary>The text is not a slash command that any source knows. It goes to the engine unchanged, so a name that nobody advertised is sent as written.</summary>
/// <param name="Text">The text, exactly as typed.</param>
public sealed record SlashPassThrough(string Text) : SlashResolution;
