namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>Either a value or a typed failure. An integrity failure never carries content.</summary>
/// <typeparam name="T">The success value.</typeparam>
public sealed class ArtifactResult<T>
    where T : class
{
    internal ArtifactResult(T? value, ArtifactFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    /// <summary>Gets the value when <see cref="IsSuccess"/> is true.</summary>
    public T? Value { get; }

    /// <summary>Gets the failure when <see cref="IsSuccess"/> is false.</summary>
    public ArtifactFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the call succeeded.</summary>
    public bool IsSuccess => Failure is null;
}
