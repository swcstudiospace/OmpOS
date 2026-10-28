namespace Autonogrammer.Api.Services.Providers;

/// <summary>Raised inside a connection attempt when a host name resolves to an address a live test must not call.</summary>
internal sealed class BlockedAddressException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="BlockedAddressException"/> class.</summary>
    public BlockedAddressException()
        : base("The host resolves to an address that is not on the public internet.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BlockedAddressException"/> class.</summary>
    /// <param name="message">The message.</param>
    public BlockedAddressException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BlockedAddressException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public BlockedAddressException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
