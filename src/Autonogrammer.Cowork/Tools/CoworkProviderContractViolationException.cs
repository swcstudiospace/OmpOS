
namespace Autonogrammer.Cowork.Tools;

/// <summary>The failed rules of a provider contract check. The message lists rule names, never arguments or secrets.</summary>
public sealed class CoworkProviderContractViolationException : Exception
{
    /// <summary>Creates the exception with no failed rules.</summary>
    public CoworkProviderContractViolationException()
        : this([])
    {
    }

    /// <summary>Creates the exception from a message. The message must not include arguments or secrets.</summary>
    /// <param name="message">The message.</param>
    public CoworkProviderContractViolationException(string message)
        : base(message)
    {
        FailedRules = [];
    }

    /// <summary>Creates the exception from a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public CoworkProviderContractViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
        FailedRules = [];
    }

    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="failedRules">The rule names that failed.</param>
    public CoworkProviderContractViolationException(IReadOnlyList<string> failedRules)
        : base("Cowork provider contract failed: " + string.Join(", ", failedRules))
    {
        FailedRules = failedRules;
    }

    /// <summary>Gets the rule names that failed.</summary>
    public IReadOnlyList<string> FailedRules { get; }
}
