namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>
/// Account facts the gate reads at step 1. The Native head implements this over Cowork's account gate.
/// Signed out, or a session that is not Cowork, is <see cref="DenialCode.Disabled"/>.
/// </summary>
public interface IComputerUseAccessFacts
{
    /// <summary><see langword="true"/> when an account session is signed in.</summary>
    bool IsSignedIn { get; }

    /// <summary><see langword="true"/> when <paramref name="sessionId"/> is a Cowork session.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <returns><see langword="true"/> for a Cowork session.</returns>
    bool IsCoworkSession(string sessionId);
}

