namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>One reviewed Chromium archive row. The embedded manifest ships with none.</summary>
/// <param name="Version">The Chromium version.</param>
/// <param name="Platform">The runtime platform id, for example <c>linux-x64</c>.</param>
/// <param name="Source">The archive location. Production accepts only <c>storage.googleapis.com</c>.</param>
/// <param name="Sha256">The hex SHA-256 of the archive bytes.</param>
/// <param name="SizeBytes">The archive size in bytes.</param>
public sealed record ChromiumManifestRow(string Version, string Platform, string Source, string Sha256, long SizeBytes);
