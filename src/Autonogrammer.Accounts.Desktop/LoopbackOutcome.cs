namespace Autonogrammer.Accounts.Desktop;

/// <summary>How a loopback link listener ended.</summary>
public enum LoopbackOutcome
{
    /// <summary>The one valid callback arrived and carried a code.</summary>
    Code,

    /// <summary>The valid address was called, but the callback reported an error. The person uses the code instead.</summary>
    LinkError,

    /// <summary>The listener's life of ten minutes ended without a valid callback. The person uses the code instead.</summary>
    Expired,
}
