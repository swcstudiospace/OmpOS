namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// An expected failure of an <see cref="IDispatchHost"/> or <see cref="IThisDeviceHost"/> call. The message is shown to the user, so a host
/// must never put a token, a secret or a device address in it.
/// </summary>
public sealed class DispatchHostException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DispatchHostException"/> class with <see cref="DispatchHostFailureKind.Unavailable"/>.</summary>
    public DispatchHostException()
    {
        Kind = DispatchHostFailureKind.Unavailable;
    }

    /// <summary>Initializes a new instance of the <see cref="DispatchHostException"/> class with <see cref="DispatchHostFailureKind.Unavailable"/>.</summary>
    /// <param name="message">The message shown to the user.</param>
    public DispatchHostException(string message)
        : base(message)
    {
        Kind = DispatchHostFailureKind.Unavailable;
    }

    /// <summary>Initializes a new instance of the <see cref="DispatchHostException"/> class with <see cref="DispatchHostFailureKind.Unavailable"/>.</summary>
    /// <param name="message">The message shown to the user.</param>
    /// <param name="innerException">The cause.</param>
    public DispatchHostException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = DispatchHostFailureKind.Unavailable;
    }

    /// <summary>Initializes a new instance of the <see cref="DispatchHostException"/> class.</summary>
    /// <param name="kind">Why the call failed.</param>
    /// <param name="message">The message shown to the user.</param>
    public DispatchHostException(DispatchHostFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>Gets why the call failed.</summary>
    public DispatchHostFailureKind Kind { get; }

    /// <summary>
    /// Maps an HTTP answer from the Dispatch service to the failure it means. A 404 with the dispatch_disabled code is the Disabled state,
    /// which an owner-token or Demo deployment also sees; any other answer is Unavailable.
    /// </summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="code">The error code in the body, when there is one.</param>
    /// <param name="message">The message shown to the user.</param>
    /// <returns>The exception to throw.</returns>
    public static DispatchHostException FromStatus(int status, string? code, string message) =>
        status == 404 && string.Equals(code, "dispatch_disabled", StringComparison.Ordinal)
            ? new DispatchHostException(DispatchHostFailureKind.Disabled, message)
            : new DispatchHostException(DispatchHostFailureKind.Unavailable, message);
}
