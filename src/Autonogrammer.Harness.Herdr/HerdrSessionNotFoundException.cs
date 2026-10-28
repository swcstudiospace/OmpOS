namespace Autonogrammer.Harness.Herdr;

/// <summary>No herdr session has the requested identifier.</summary>
public sealed class HerdrSessionNotFoundException : HerdrSessionException
{
    /// <summary>Initializes a new instance of the <see cref="HerdrSessionNotFoundException"/> class.</summary>
    public HerdrSessionNotFoundException()
        : base("No herdr session has the requested identifier.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HerdrSessionNotFoundException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public HerdrSessionNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HerdrSessionNotFoundException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public HerdrSessionNotFoundException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
