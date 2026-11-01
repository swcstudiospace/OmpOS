namespace Autonogrammer.Resilience.Tests;

/// <summary>A failure the test policies classify as a serialisation failure, the stand-in for SQLSTATE 40001.</summary>
internal sealed class SerializationFault : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SerializationFault"/> class.</summary>
    public SerializationFault()
        : base("serialization fault")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SerializationFault"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public SerializationFault(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SerializationFault"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public SerializationFault(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
