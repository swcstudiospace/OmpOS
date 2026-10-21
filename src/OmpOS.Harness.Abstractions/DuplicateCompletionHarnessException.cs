namespace OmpOS.Harness.Abstractions;

/// <summary>A prompt stream emitted more than one completion event.</summary>
public sealed class DuplicateCompletionHarnessException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="DuplicateCompletionHarnessException"/> class.</summary>
    public DuplicateCompletionHarnessException()
        : base("The prompt stream emitted more than one completion.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DuplicateCompletionHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public DuplicateCompletionHarnessException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DuplicateCompletionHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public DuplicateCompletionHarnessException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DuplicateCompletionHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="context">Optional context.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    public DuplicateCompletionHarnessException(string message, string? context = null, Exception? innerException = null)
        : base(message, context, innerException)
    {
    }
}
