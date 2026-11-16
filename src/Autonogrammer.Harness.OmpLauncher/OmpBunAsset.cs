namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>One Bun release archive for one platform, with the SHA-256 published for it.</summary>
/// <param name="FileName">The archive name in the release, such as <c>bun-linux-x64.zip</c>.</param>
/// <param name="Sha256">The hex SHA-256 of the archive.</param>
public sealed record OmpBunAsset(string FileName, string Sha256);
