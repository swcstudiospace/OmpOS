namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Closed frame type names. An unknown <c>t</c> is <see cref="UnsupportedFrame"/>, never executed.</summary>
public static class DispatchFrameTypes
{
    /// <summary>Hub to device handshake challenge.</summary>
    public const string Challenge = "challenge";

    /// <summary>Hub to device welcome.</summary>
    public const string Welcome = "welcome";

    /// <summary>Hub to device signed command.</summary>
    public const string Cmd = "cmd";

    /// <summary>Hub to device revocation.</summary>
    public const string Revoke = "revoke";

    /// <summary>Either direction error.</summary>
    public const string Err = "err";

    /// <summary>Device to hub handshake proof.</summary>
    public const string Hello = "hello";

    /// <summary>Device to hub acknowledgement.</summary>
    public const string Ack = "ack";

    /// <summary>Device to hub progress event.</summary>
    public const string Evt = "evt";

    /// <summary>Device to hub terminal result.</summary>
    public const string Res = "res";

    /// <summary>Device to hub heartbeat.</summary>
    public const string Hb = "hb";

    /// <summary>Error code for an unknown <c>t</c>. Not a <see cref="DispatchFailureReason"/>.</summary>
    public const string UnsupportedFrame = "unsupported_frame";

    /// <summary>Largest accepted text frame, 1 MiB.</summary>
    public const int MaxFrameBytes = 1_048_576;

    /// <summary>Reports whether <paramref name="type"/> is a closed frame type.</summary>
    /// <param name="type">The <c>t</c> value.</param>
    /// <returns><see langword="true"/> when the type is known.</returns>
    public static bool IsKnown(string? type) => type is
        Challenge or Welcome or Cmd or Revoke or Err or Hello or Ack or Evt or Res or Hb;
}
