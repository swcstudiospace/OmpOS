namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>Factories for <see cref="ArtifactResult{T}"/>. Static members stay off the generic type.</summary>
public static class ArtifactResults
{
    /// <summary>Creates a success.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>A successful result.</returns>
    public static ArtifactResult<T> Ok<T>(T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ArtifactResult<T>(value, null);
    }

    /// <summary>Creates a failure with no value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="failure">The typed failure.</param>
    /// <returns>A failed result.</returns>
    public static ArtifactResult<T> Fail<T>(ArtifactFailure failure)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ArtifactResult<T>(null, failure);
    }
}
