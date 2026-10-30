namespace Autonogrammer.Presentation.Commands;

/// <summary>One request to run a native action.</summary>
/// <param name="ActionId">The action, one of <see cref="SlashActionIds"/>.</param>
/// <param name="Argument">The fixed argument the catalogue gives the action (a settings section, a mode, a model filter), or an empty string.</param>
/// <param name="TypedText">The text the user typed after the command, without the leading space; empty when there is none.</param>
public sealed record SlashActionRequest(string ActionId, string Argument, string TypedText);
