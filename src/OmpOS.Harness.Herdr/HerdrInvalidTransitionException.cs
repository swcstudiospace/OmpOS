namespace OmpOS.Harness.Herdr;

/// <summary>The requested operation is not allowed in the session's current state.</summary>
public sealed class HerdrInvalidTransitionException : HerdrSessionException
{
    /// <summary>Initializes a new instance of the <see cref="HerdrInvalidTransitionException"/> class.</summary>
    public HerdrInvalidTransitionException()
        : base("The herdr session state does not allow this operation.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HerdrInvalidTransitionException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public HerdrInvalidTransitionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HerdrInvalidTransitionException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public HerdrInvalidTransitionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
