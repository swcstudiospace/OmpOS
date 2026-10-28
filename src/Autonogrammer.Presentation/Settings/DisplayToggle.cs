using Autonogrammer.Presentation.Commands.Handlers;

namespace Autonogrammer.Presentation.Settings;

/// <summary>One switch of the Display page: which display setting it sets, its label and the sentence that says what it does.</summary>
/// <param name="Setting">The display setting the switch sets.</param>
/// <param name="Label">The switch label; it is also the accessible name.</param>
/// <param name="Help">The sentence under the label.</param>
public sealed record DisplayToggle(DisplaySetting Setting, string Label, string Help);
