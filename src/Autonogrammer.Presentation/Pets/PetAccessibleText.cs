namespace Autonogrammer.Presentation.Pets;

/// <summary>The text equivalents of the companion: what a screen reader announces for each <see cref="PetState"/> and for the hide toggle.</summary>
public static class PetAccessibleText
{
    /// <summary>Returns the sentence that stands in for the companion in a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>A non-empty sentence. A value outside <see cref="PetState"/> reads as the fallback state.</returns>
    public static string TextOf(PetState state) => state switch
    {
        PetState.Hidden => "Companion hidden.",
        PetState.Idle => "Companion is resting. No agent is working or waiting for you.",
        PetState.Working => "Companion is busy. An agent is working.",
        PetState.AttentionNeeded => "Companion is asking for you. An agent needs your attention.",
        _ => "Companion status is unavailable. Agent status could not be read.",
    };

    /// <summary>Returns the label of the hide toggle.</summary>
    /// <param name="isHidden">True when the companion is hidden now.</param>
    /// <returns><c>Show companion</c> when hidden, otherwise <c>Hide companion</c>.</returns>
    public static string ToggleLabelOf(bool isHidden) => isHidden ? "Show companion" : "Hide companion";
}
