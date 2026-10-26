namespace OmpOS.Presentation.Terminal;

/// <summary>
/// Whether a head can run a local terminal and, when it cannot, why. The reason is shown to the user in place of the
/// local terminal, which is never created.
/// </summary>
/// <param name="IsAvailable">True when the head may spawn a local shell.</param>
/// <param name="Reason">The user-facing reason a local terminal is unavailable; empty when <paramref name="IsAvailable"/> is true.</param>
public sealed record TerminalAvailability(bool IsAvailable, string Reason)
{
    /// <summary>The reason shown on a mobile head.</summary>
    public const string MobileReason = "A local terminal is not available on mobile: the platform does not allow this app to start a shell. Attach to a remote terminal instead.";

    /// <summary>The reason shown on the web head.</summary>
    public const string WebReason = "A local terminal is not available in the browser: a web page cannot start a process. Attach to a remote terminal instead.";

    /// <summary>Resolves the local-terminal availability of a head.</summary>
    /// <param name="host">The kind of head.</param>
    /// <returns>Available on desktop; unavailable with a reason on mobile and web.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="host"/> is not a defined <see cref="TerminalHostKind"/>.</exception>
    public static TerminalAvailability ForLocal(TerminalHostKind host) => host switch
    {
        TerminalHostKind.Desktop => new TerminalAvailability(true, string.Empty),
        TerminalHostKind.Mobile => new TerminalAvailability(false, MobileReason),
        TerminalHostKind.Web => new TerminalAvailability(false, WebReason),
        _ => throw new ArgumentOutOfRangeException(nameof(host), host, "Unknown terminal host kind."),
    };
}
