namespace Autonogrammer.Harness.Abstractions;

/// <summary>The outcome of <see cref="UserDataDirectory.EnsureCurrent"/>.</summary>
/// <param name="Path">The per-user data directory every site uses. It may not exist yet: the stores create it on their first write.</param>
/// <param name="Migrated">Whether this call moved the legacy directory to <paramref name="Path"/>. False when nothing needed moving or another process did it.</param>
/// <param name="Warning">A one-line warning for the user, or null. It names directories only and never carries file contents.</param>
public sealed record UserDataMigration(string Path, bool Migrated, string? Warning);
