namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// The operating-system secret store cannot hold or return the device key or credential. Pairing is refused and a paired desktop does
/// not connect. There is no file fallback. The message names no value.
/// </summary>
public sealed class DispatchKeyStoreUnavailableException : Exception
{
    private const string DefaultMessage = "The secret store that holds the device key is unavailable. Dispatch is not paired or connected.";

    /// <summary>Initializes a new instance.</summary>
    public DispatchKeyStoreUnavailableException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The message. It must name no secret value.</param>
    public DispatchKeyStoreUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="inner">The store failure.</param>
    public DispatchKeyStoreUnavailableException(Exception inner)
        : base(DefaultMessage, inner)
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The message. It must name no secret value.</param>
    /// <param name="inner">The store failure.</param>
    public DispatchKeyStoreUnavailableException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
