namespace Autonogrammer.Cowork.Chat;

/// <summary>The Cowork start is refused because the prompt file cannot be written. There is no Code prompt fallback.</summary>
public sealed class CoworkPromptUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CoworkPromptUnavailableException"/> class.</summary>
    public CoworkPromptUnavailableException()
        : this("The Cowork prompt file cannot be written.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CoworkPromptUnavailableException"/> class.</summary>
    /// <param name="reason">Why the file cannot be written. It is the message the start reports.</param>
    public CoworkPromptUnavailableException(string reason)
        : base(reason)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CoworkPromptUnavailableException"/> class.</summary>
    /// <param name="reason">Why the file cannot be written. It is the message the start reports.</param>
    /// <param name="innerException">The failure that caused the refusal.</param>
    public CoworkPromptUnavailableException(string reason, Exception innerException)
        : base(reason, innerException)
    {
    }
}
