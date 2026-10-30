namespace Autonogrammer.Harness.Providers;

/// <summary>
/// A failed provider request as the caller observed it. The detail is the provider's own error text when the body had one.
/// The plan end date is the last day the user's coding plan is valid, when the application knows it; it is the user's own
/// subscription state, which the catalogue cannot know.
/// </summary>
/// <param name="HttpStatus">The HTTP status the provider answered with.</param>
/// <param name="Detail">The provider's error message text, or null when the answer had none.</param>
/// <param name="PlanValidUntil">The last day the user's coding plan is valid, or null when it is not known.</param>
public sealed record ProviderFailure(int HttpStatus, string? Detail = null, DateOnly? PlanValidUntil = null);
