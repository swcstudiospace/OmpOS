namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>Where the locator stopped, and the binary when one was found.</summary>
/// <param name="State">The locate state.</param>
/// <param name="ExecutablePath">The binary path when <paramref name="State"/> is <see cref="BrowserLocateState.Found"/> or <see cref="BrowserLocateState.BrowserTooOld"/>.</param>
/// <param name="MajorVersion">The observed major version, when a binary was read.</param>
public sealed record ChromiumLocation(BrowserLocateState State, string? ExecutablePath, int? MajorVersion);
