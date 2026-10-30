namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>The outcome of changing an Autonogrammer-owned file. A failure never throws and never damages the file.</summary>
/// <param name="Succeeded">True when the file now says what was asked (or already did).</param>
/// <param name="Error">The reason it could not be changed, or null.</param>
public sealed record SafetyEditResult(bool Succeeded, string? Error)
{
    /// <summary>Gets the successful result.</summary>
    public static SafetyEditResult Success { get; } = new(true, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The reason.</param>
    /// <returns>The result.</returns>
    public static SafetyEditResult Failure(string error) => new(false, error);
}
