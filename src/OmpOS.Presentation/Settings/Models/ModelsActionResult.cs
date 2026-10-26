namespace OmpOS.Presentation.Settings.Models;

/// <summary>The result of one write the source made for the Models page.</summary>
/// <param name="Succeeded">True when omp now uses the change.</param>
/// <param name="Message">The reason for a failure, already mapped to text for the user, or null. It must not contain a key; the page also hides any key it finds.</param>
public sealed record ModelsActionResult(bool Succeeded, string? Message = null)
{
    /// <summary>Gets a successful result.</summary>
    public static ModelsActionResult Success { get; } = new(true);

    /// <summary>Creates a failed result.</summary>
    /// <param name="message">The reason, worded for the user.</param>
    /// <returns>The failed result.</returns>
    public static ModelsActionResult Failure(string message) => new(false, message);
}
