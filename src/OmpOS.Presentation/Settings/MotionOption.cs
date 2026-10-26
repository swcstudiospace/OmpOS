namespace OmpOS.Presentation.Settings;

/// <summary>One choice of the motion setting, with the texts a view shows for it.</summary>
/// <param name="Value">The motion preference the choice selects.</param>
/// <param name="Label">The short label.</param>
/// <param name="Description">The sentence that says what the choice does.</param>
public sealed record MotionOption(MotionPreference Value, string Label, string Description);
