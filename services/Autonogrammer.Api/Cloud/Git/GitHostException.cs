namespace Autonogrammer.Api.Cloud;

/// <summary>A git host call failed. The message is written by Autonogrammer and never repeats a token, a URL with a credential or the host's response body.</summary>
internal sealed class GitHostException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GitHostException"/> class for an unavailable host.</summary>
    public GitHostException()
        : this(GitHostFailure.Unavailable, "The git host call failed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GitHostException"/> class for an unavailable host.</summary>
    /// <param name="message">A safe explanation.</param>
    public GitHostException(string message)
        : this(GitHostFailure.Unavailable, message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GitHostException"/> class for an unavailable host.</summary>
    /// <param name="message">A safe explanation.</param>
    /// <param name="innerException">The underlying failure.</param>
    public GitHostException(string message, Exception innerException)
        : this(GitHostFailure.Unavailable, message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GitHostException"/> class.</summary>
    /// <param name="failure">Why the call failed.</param>
    /// <param name="message">A safe explanation.</param>
    public GitHostException(GitHostFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    /// <summary>Initializes a new instance of the <see cref="GitHostException"/> class.</summary>
    /// <param name="failure">Why the call failed.</param>
    /// <param name="message">A safe explanation.</param>
    /// <param name="innerException">The underlying failure.</param>
    public GitHostException(GitHostFailure failure, string message, Exception innerException)
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>Gets why the call failed.</summary>
    public GitHostFailure Failure { get; }
}
