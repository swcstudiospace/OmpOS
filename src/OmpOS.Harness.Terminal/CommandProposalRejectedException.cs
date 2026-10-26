namespace OmpOS.Harness.Terminal;

/// <summary>
/// Thrown when a proposal names a working directory that is outside the workspace root or is not an existing directory.
/// The proposal is not recorded and nothing is spawned.
/// </summary>
public sealed class CommandProposalRejectedException : Exception
{
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public CommandProposalRejectedException()
    {
    }

    /// <summary>
    /// Initializes a new instance with a message that names the rejected working directory.
    /// </summary>
    /// <param name="message">The reason the proposal was rejected.</param>
    public CommandProposalRejectedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The reason the proposal was rejected.</param>
    /// <param name="innerException">The exception that caused the rejection.</param>
    public CommandProposalRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
