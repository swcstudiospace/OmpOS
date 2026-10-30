namespace Autonogrammer.Api.Cloud;

/// <summary>The result of a control plane operation: a value, or the <see cref="CloudProblem"/> that refused it.</summary>
/// <typeparam name="T">The value type.</typeparam>
internal sealed class CloudOutcome<T>
{
    private CloudOutcome(T? value, CloudProblem? problem)
    {
        Value = value;
        Problem = problem;
    }

    /// <summary>Gets the value, or null when the operation was refused.</summary>
    public T? Value { get; }

    /// <summary>Gets the refusal, or null when the operation succeeded.</summary>
    public CloudProblem? Problem { get; }

    /// <summary>Gets whether the operation succeeded.</summary>
    public bool Succeeded => Problem is null;

    /// <summary>Wraps a value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A successful outcome.</returns>
    public static CloudOutcome<T> Ok(T value) => new(value, null);

    /// <summary>Wraps a refusal.</summary>
    /// <param name="problem">The refusal.</param>
    /// <returns>A refused outcome.</returns>
    public static CloudOutcome<T> Fail(CloudProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new CloudOutcome<T>(default, problem);
    }
}
