namespace Autonogrammer.Presentation.Tests.Hooks;

/// <summary>An out-of-memory fault as the runtime raises it, for a write the model must let escape.</summary>
internal sealed class SimulatedOutOfMemoryException : OutOfMemoryException
{
    /// <summary>Creates the fault with the runtime's default message.</summary>
    public SimulatedOutOfMemoryException()
    {
    }

    /// <summary>Creates the fault with a message.</summary>
    /// <param name="message">The message.</param>
    public SimulatedOutOfMemoryException(string? message)
        : base(message)
    {
    }

    /// <summary>Creates the fault with a message and the exception that caused it.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SimulatedOutOfMemoryException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
