namespace OmpOS.Presentation.Settings.Models;

/// <summary>The outcome of one connection test.</summary>
/// <param name="Status">What the test found.</param>
/// <param name="Message">A sentence for the user, already mapped by the source; empty when the page should use its own wording. It must not contain a key.</param>
public sealed record ProviderTestOutcome(ProviderTestStatus Status, string Message);
