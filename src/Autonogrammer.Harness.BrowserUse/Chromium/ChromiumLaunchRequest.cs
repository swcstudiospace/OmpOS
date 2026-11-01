namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>What the host asks the launcher to start. Arguments are already a list.</summary>
/// <param name="ExecutablePath">The Chromium binary.</param>
/// <param name="Arguments">The flag list from <see cref="ChromiumLaunchPlan"/>.</param>
/// <param name="Environment">The allow-listed environment.</param>
/// <param name="ProfileDirectory">The fresh profile directory.</param>
public sealed record ChromiumLaunchRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    string ProfileDirectory);
