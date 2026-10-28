namespace OmpOS.Presentation.Mentions;

/// <summary>
/// Thrown by a mention source when what the user picked cannot be attached: the file is binary or gone, the repository has no diff, the host is not
/// configured. The message is shown to the user as written, so it must be a plain sentence.
/// </summary>
public sealed class MentionUnavailableException : Exception
{
    /// <summary>Creates the exception with the sentence the user will read.</summary>
    /// <param name="message">The reason, as a plain sentence.</param>
    public MentionUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with no message. The default message reads <see cref="MentionCopy.CannotAttach"/>.</summary>
    public MentionUnavailableException()
        : base(MentionCopy.CannotAttach)
    {
    }

    /// <summary>Creates the exception with the sentence the user will read and the failure behind it.</summary>
    /// <param name="message">The reason, as a plain sentence.</param>
    /// <param name="innerException">The failure behind it.</param>
    public MentionUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
