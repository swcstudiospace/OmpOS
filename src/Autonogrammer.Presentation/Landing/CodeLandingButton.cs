namespace Autonogrammer.Presentation.Landing;

/// <summary>A button of a card.</summary>
/// <param name="Label">The text of the button.</param>
/// <param name="AccessibleName">The name a screen reader announces.</param>
/// <param name="IsEnabled">Whether the button can be activated.</param>
/// <param name="Action">What activating the button asks for, or null for a button that only shows a name.</param>
/// <param name="OptionId">The identifier of the environment the action starts on, or null.</param>
public sealed record CodeLandingButton(string Label, string AccessibleName, bool IsEnabled, CodeLandingAction? Action, string? OptionId = null);
