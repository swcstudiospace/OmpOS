namespace Autonogrammer.Harness.Providers;

/// <summary>The outcome of a validation. An empty error list means the subject is accepted.</summary>
public sealed class ProviderValidationResult
{
    /// <summary>Creates a result from the errors found, in the order the checks ran.</summary>
    /// <param name="errors">Every rule the subject broke. Empty when the subject is valid.</param>
    public ProviderValidationResult(IReadOnlyList<ProviderValidationError> errors)
    {
        Errors = errors;
    }

    /// <summary>Every rule the subject broke.</summary>
    public IReadOnlyList<ProviderValidationError> Errors { get; }

    /// <summary>True when no rule was broken.</summary>
    public bool IsValid => Errors.Count == 0;
}
