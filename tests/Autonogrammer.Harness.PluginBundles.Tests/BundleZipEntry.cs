namespace Autonogrammer.Harness.PluginBundles.Tests;

/// <summary>One zip entry written at test time. Nothing here is committed as an archive.</summary>
/// <param name="Name">The entry name, which may be hostile.</param>
/// <param name="Content">The entry bytes.</param>
/// <param name="Symlink">Whether the entry is marked as a Unix symbolic link.</param>
internal sealed record BundleZipEntry(string Name, IReadOnlyList<byte> Content, bool Symlink = false);
