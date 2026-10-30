namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>The result of one acquisition attempt.</summary>
/// <param name="Outcome">Why it finished.</param>
/// <param name="PublishedDirectory">The published directory when <paramref name="Outcome"/> is <see cref="AcquireOutcome.Installed"/>.</param>
public sealed record AcquireResult(AcquireOutcome Outcome, string? PublishedDirectory);
