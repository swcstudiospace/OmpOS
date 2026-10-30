namespace Autonogrammer.Presentation.Settings;

/// <summary>One choice of the theme setting, with the texts a view shows for it.</summary>
/// <param name="Value">The theme preference the choice selects.</param>
/// <param name="Label">The short label.</param>
/// <param name="Description">The sentence that says what the choice does.</param>
public sealed record ThemeOption(ThemePreference Value, string Label, string Description);
