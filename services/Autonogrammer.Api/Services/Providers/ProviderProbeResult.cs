using Autonogrammer.Contracts.Providers;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>What a live test found at an address.</summary>
/// <param name="Status">The outcome.</param>
/// <param name="Reason">A sentence for a result other than reachable, or null. It never repeats a reply of the provider.</param>
internal sealed record ProviderProbeResult(ProviderTestStatus Status, string? Reason = null);
