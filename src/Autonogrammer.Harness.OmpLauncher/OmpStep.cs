namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// One pipeline step's outcome: either its value, or the terminal result the pipeline returns.
/// </summary>
/// <typeparam name="T">The value the step produces.</typeparam>
/// <param name="Value">The value, when the step succeeded.</param>
/// <param name="Failure">The terminal result, when the step did not succeed.</param>
internal readonly record struct OmpStep<T>(T? Value, OmpUpdateResult? Failure)
{
    /// <summary>A successful step.</summary>
    /// <param name="value">The produced value.</param>
    /// <returns>The step.</returns>
    public static OmpStep<T> Ok(T value) => new(value, null);

    /// <summary>A step that ends the pipeline.</summary>
    /// <param name="failure">The terminal result.</param>
    /// <returns>The step.</returns>
    public static OmpStep<T> Fail(OmpUpdateResult failure) => new(default, failure);
}
