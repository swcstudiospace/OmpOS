namespace OmpOS.Harness.Herdr;

/// <summary>
/// Base type of every typed herdr session failure.
/// </summary>
public abstract class HerdrSessionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="HerdrSessionException"/> class.</summary>
    protected HerdrSessionException()
        : this("A herdr session failure occurred.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HerdrSessionException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    protected HerdrSessionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HerdrSessionException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    protected HerdrSessionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
