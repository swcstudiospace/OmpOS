namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// Implemented by an <see cref="IOmpSession"/> whose engine names its own session, so a host can persist that name and resume the
/// conversation after a restart.
/// </summary>
public interface IOmpSessionIdentity
{
    /// <summary>Gets the identifier the engine gave the session, or <see langword="null"/> until the session has been created or resumed.</summary>
    string? NativeSessionId { get; }
}
