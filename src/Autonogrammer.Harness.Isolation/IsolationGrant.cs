namespace Autonogrammer.Harness.Isolation;

/// <summary>A folder the session may read, and may write only when <paramref name="ReadWrite"/> is true.</summary>
/// <param name="Path">The absolute folder path.</param>
/// <param name="ReadWrite">True when the folder is a read-write grant. A read-only grant is not a write bind.</param>
public sealed record IsolationGrant(string Path, bool ReadWrite);
