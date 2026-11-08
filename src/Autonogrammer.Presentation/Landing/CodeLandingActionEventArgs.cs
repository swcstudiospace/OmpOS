namespace Autonogrammer.Presentation.Landing;

/// <summary>The request a card button made.</summary>
/// <param name="action">What to do.</param>
/// <param name="optionId">The environment the action starts on, or null.</param>
public sealed class CodeLandingActionEventArgs(CodeLandingAction action, string? optionId) : EventArgs
{
    /// <summary>Gets what to do.</summary>
    public CodeLandingAction Action { get; } = action;

    /// <summary>Gets the environment the action starts on, or null.</summary>
    public string? OptionId { get; } = optionId;
}
