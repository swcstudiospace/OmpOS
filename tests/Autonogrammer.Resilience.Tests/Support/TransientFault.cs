namespace Autonogrammer.Resilience.Tests;

/// <summary>A failure the test policies classify as transient, the stand-in for a dropped connection.</summary>
internal sealed class TransientFault : Exception
{
    /// <summary>Initializes a new instance of the <see cref="TransientFault"/> class.</summary>
    public TransientFault()
        : base("transient fault")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TransientFault"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public TransientFault(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TransientFault"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public TransientFault(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
