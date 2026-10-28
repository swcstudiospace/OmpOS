namespace Autonogrammer.Harness.Registry;

/// <summary>
/// The outcome of reading an export.
/// </summary>
public enum RegistryReadStatus
{
    /// <summary>
    /// The export is valid and its generation equals the reader's expected generation.
    /// </summary>
    Current,

    /// <summary>
    /// The export is valid but its generation differs from the reader's expected generation.
    /// </summary>
    Drift,

    /// <summary>
    /// No export exists at the path.
    /// </summary>
    Missing,

    /// <summary>
    /// The export exists but was refused. See <see cref="RegistryReadResult.Rejection"/>.
    /// </summary>
    Rejected,
}
