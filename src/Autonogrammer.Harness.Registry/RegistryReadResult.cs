namespace Autonogrammer.Harness.Registry;

/// <summary>
/// The result of reading an export. <see cref="Snapshot"/> is set for <see cref="RegistryReadStatus.Current"/> and
/// <see cref="RegistryReadStatus.Drift"/>, and <see cref="Rejection"/> is set for <see cref="RegistryReadStatus.Rejected"/>.
/// Both are null for <see cref="RegistryReadStatus.Missing"/>.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="Snapshot">The export's generation and entries, when the export was accepted.</param>
/// <param name="Rejection">Why the export was refused, when it was.</param>
public sealed record RegistryReadResult(RegistryReadStatus Status, RegistrySnapshot? Snapshot, RegistryRejectionKind? Rejection);
