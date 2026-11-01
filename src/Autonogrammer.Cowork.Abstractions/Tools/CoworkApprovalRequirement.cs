namespace Autonogrammer.Cowork.Abstractions;

/// <summary>Who decides whether a call may run.</summary>
public enum CoworkApprovalRequirement
{
    /// <summary>The permission mode and stored grants decide.</summary>
    ByPolicy = 0,

    /// <summary>Ask on every call in every mode. No grant satisfies it.</summary>
    EveryCall = 1,

    /// <summary>Ask on the machine that runs the tool, every call, every origin, every mode.</summary>
    EveryCallOnExecutingDevice = 2,

    /// <summary>
    /// The host neither asks nor skips. The provider's own gate decides through the broker it is given.
    /// The host still applies unavailable as deny, MustBeLocal for every answer the provider requests, audit and events.
    /// </summary>
    ByProviderGate = 3,
}
