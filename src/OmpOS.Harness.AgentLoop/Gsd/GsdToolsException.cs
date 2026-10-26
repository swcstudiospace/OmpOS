namespace OmpOS.Harness.AgentLoop.Gsd;

/// <summary>
/// A typed failure of a workflow engine query. The message is plain text and never contains raw tool output, so it is safe
/// to show to the operator.
/// </summary>
public sealed class GsdToolsException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GsdToolsException"/> class.</summary>
    public GsdToolsException()
        : this(GsdToolsFailure.InvalidOutput, "The workflow tool failed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GsdToolsException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public GsdToolsException(string message)
        : this(GsdToolsFailure.InvalidOutput, message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GsdToolsException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public GsdToolsException(string message, Exception? innerException)
        : this(GsdToolsFailure.InvalidOutput, message, innerException: innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GsdToolsException"/> class.</summary>
    /// <param name="failure">The failure category.</param>
    /// <param name="message">The plain-text failure message. It must not contain raw tool output.</param>
    /// <param name="exitCode">The process exit code, or null when there is none.</param>
    /// <param name="innerException">The underlying exception.</param>
    public GsdToolsException(GsdToolsFailure failure, string message, int? exitCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
        ExitCode = exitCode;
    }

    /// <summary>Gets the failure category.</summary>
    public GsdToolsFailure Failure { get; }

    /// <summary>Gets the exit code of the tool process, or null when the failure is not an exit code.</summary>
    public int? ExitCode { get; }
}
