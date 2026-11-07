namespace Autonogrammer.Presentation.Dispatch;

/// <summary>Why a Dispatch host call failed. Each kind has its own state in the views.</summary>
public enum DispatchHostFailureKind
{
    /// <summary>The service cannot be reached or answered with an error.</summary>
    Unavailable,

    /// <summary>The head has no Dispatch service configured.</summary>
    NotConfigured,

    /// <summary>The user is not signed in.</summary>
    SignedOut,

    /// <summary>Dispatch is off for this account or deployment.</summary>
    Disabled,

    /// <summary>The request was refused.</summary>
    Rejected,
}
