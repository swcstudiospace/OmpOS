using Autonogrammer.Api.Services.Providers;
using Autonogrammer.Contracts.Providers;

namespace Autonogrammer.Api.IntegrationTests.Providers;

/// <summary>A probe that answers a scripted status and records the addresses it was asked to call.</summary>
internal sealed class FakeProviderProbe : IProviderProbe
{
    /// <summary>Gets or sets what every probe finds.</summary>
    public ProviderProbeResult Result { get; set; } = new(ProviderTestStatus.Reachable);

    /// <summary>Gets the addresses called.</summary>
    public List<Uri> Calls { get; } = [];

    /// <inheritdoc />
    public Task<ProviderProbeResult> ProbeAsync(Uri url, CancellationToken cancellationToken)
    {
        Calls.Add(url);
        return Task.FromResult(Result);
    }
}
