using System.Diagnostics.Metrics;
using Autonogrammer.Api.Telemetry;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>The artifact counter on <see cref="ApiTelemetry.SourceName"/>.</summary>
internal sealed class ArtifactMetrics : IDisposable
{
    /// <summary>The counter name.</summary>
    public const string CountName = "autonogrammer.cowork.artifact.count";

    private readonly Meter _meter;
    private readonly Counter<long> _count;

    /// <summary>Initializes a new instance of the <see cref="ArtifactMetrics"/> class.</summary>
    /// <param name="meterFactory">The meter factory.</param>
    public ArtifactMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(ApiTelemetry.SourceName);
        _count = _meter.CreateCounter<long>(CountName, description: "Artifact creates, updates, rejections and conflicts.");
    }

    /// <summary>Adds one outcome. Tags are <c>kind</c> and <c>result</c>.</summary>
    /// <param name="kind">The artifact kind name, or the outcome when no kind is known.</param>
    /// <param name="result">One of <c>created</c>, <c>updated</c>, <c>rejected</c> or <c>conflict</c>.</param>
    public void Add(string kind, string result) => _count.Add(1, new KeyValuePair<string, object?>("kind", kind), new KeyValuePair<string, object?>("result", result));

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
