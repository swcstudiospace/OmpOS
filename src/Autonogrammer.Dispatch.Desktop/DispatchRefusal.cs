namespace Autonogrammer.Dispatch.Desktop;

/// <summary>Why a hub command was refused before any handler ran. A refused frame is never acknowledged and never executed.</summary>
public enum DispatchRefusal
{
    /// <summary>A signed field is empty or outside <c>[A-Za-z0-9._:-]</c>, or the frame names an unknown or non-command capability or operation.</summary>
    Malformed,

    /// <summary>The frame's user is not the account stored at pairing.</summary>
    WrongUser,

    /// <summary>The frame is addressed to another device.</summary>
    WrongDevice,

    /// <summary>The signing key identifier was not delivered at pairing.</summary>
    UnknownKid,

    /// <summary>The frame expired by the hub clock corrected for the measured offset and the allowed skew.</summary>
    Expired,

    /// <summary>The payload's SHA-256 does not match the signed payload hash.</summary>
    PayloadMismatch,

    /// <summary>The P-1363 ES256 signature does not verify over the canonical bytes.</summary>
    SignatureInvalid,
}
