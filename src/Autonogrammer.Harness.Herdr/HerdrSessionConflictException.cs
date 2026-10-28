namespace Autonogrammer.Harness.Herdr;

/// <summary>A session with the requested identifier already exists and cannot be started again.</summary>
public sealed class HerdrSessionConflictException : HerdrSessionException
{
    /// <summary>Initializes a new instance of the <see cref="HerdrSessionConflictException"/> class.</summary>
    public HerdrSessionConflictException()
        : base("A herdr session with the requested identifier already exists.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HerdrSessionConflictException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public HerdrSessionConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HerdrSessionConflictException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public HerdrSessionConflictException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
