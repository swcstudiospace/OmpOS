namespace Autonogrammer.Cowork.Abstractions;

/// <summary>Whether a provider can serve a session, and why not when it cannot.</summary>
/// <param name="Availability">The availability.</param>
/// <param name="Reason">A short reason when the provider is not available, or null.</param>
public sealed record CoworkProviderState(CoworkProviderAvailability Availability, string? Reason);
