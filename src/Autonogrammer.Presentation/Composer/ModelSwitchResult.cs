namespace Autonogrammer.Presentation.Composer;

/// <summary>What the host answers when the model picker asks it to switch the model of the session.</summary>
/// <param name="IsSwitched">Whether the engine now uses the requested model.</param>
/// <param name="CurrentValue">The identifier of the model the engine uses after the attempt, or <see langword="null"/> when the host does not know it.</param>
/// <param name="Message">A plain sentence that says why the model did not change, or <see langword="null"/> after a switch.</param>
public sealed record ModelSwitchResult(bool IsSwitched, string? CurrentValue, string? Message)
{
    /// <summary>Creates the result of a switch the engine confirmed.</summary>
    /// <param name="currentValue">The identifier of the model now in use.</param>
    /// <returns>The result.</returns>
    public static ModelSwitchResult Switched(string currentValue) => new(true, currentValue, null);

    /// <summary>Creates the result of a switch that did not happen.</summary>
    /// <param name="message">Why the model did not change, as a plain sentence.</param>
    /// <param name="currentValue">The identifier of the model still in use, when the host knows it.</param>
    /// <returns>The result.</returns>
    public static ModelSwitchResult Refused(string message, string? currentValue = null) => new(false, currentValue, message);
}
