namespace Autonogrammer.Presentation.Cowork;

/// <summary>Which signed-out panel a Cowork head shows.</summary>
public enum CoworkSignedOutState
{
    /// <summary>The API has a client sign-in block. The panel shows the signed-out sentence and the sign-in button.</summary>
    SignInAvailable,

    /// <summary>The API has no client sign-in block. The panel shows the not-configured sentence and no button.</summary>
    NotConfigured,
}
