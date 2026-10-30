namespace Autonogrammer.Presentation.Composer;

/// <summary>What the host answers when an option chip asks it to change the value of a session option.</summary>
/// <param name="IsSet">Whether the engine now uses the requested value.</param>
/// <param name="CurrentValue">The value the engine uses after the attempt, or <see langword="null"/> when the host does not know it.</param>
/// <param name="Message">A plain sentence that says why the value did not change, or <see langword="null"/> after a change.</param>
public sealed record OptionChangeResult(bool IsSet, string? CurrentValue, string? Message)
{
    /// <summary>Creates the result of a change the engine confirmed.</summary>
    /// <param name="currentValue">The value now in use.</param>
    /// <returns>The result.</returns>
    public static OptionChangeResult Set(string currentValue) => new(true, currentValue, null);

    /// <summary>Creates the result of a change that did not happen.</summary>
    /// <param name="message">Why the value did not change, as a plain sentence.</param>
    /// <param name="currentValue">The value still in use, when the host knows it.</param>
    /// <returns>The result.</returns>
    public static OptionChangeResult Refused(string message, string? currentValue = null) => new(false, currentValue, message);
}
