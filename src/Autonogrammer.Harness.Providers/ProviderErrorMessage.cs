namespace Autonogrammer.Harness.Providers;

/// <summary>A failure translated into text the user can act on, with the page where the action is taken.</summary>
/// <param name="Kind">What the failure means.</param>
/// <param name="Message">A sentence or two that names the provider, says what happened and says what to do next.</param>
/// <param name="ActionUrl">The page where the user buys, renews or checks the plan, or null when the entry has none.</param>
public sealed record ProviderErrorMessage(ProviderErrorKind Kind, string Message, Uri? ActionUrl);
