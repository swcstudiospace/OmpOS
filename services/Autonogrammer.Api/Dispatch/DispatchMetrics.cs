using System.Diagnostics.Metrics;
using Autonogrammer.Api.Telemetry;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Dispatch instruments on the API meter (<see cref="ApiTelemetry.SourceName"/>). Tags are only <c>result</c> and <c>reason</c>.
/// </summary>
internal sealed class DispatchMetrics : IDisposable
{
    /// <summary>The pairing counter name.</summary>
    public const string PairingName = "autonogrammer.dispatch.pairing";

    /// <summary>The connect counter name.</summary>
    public const string ConnectName = "autonogrammer.dispatch.connect";

    private readonly Meter _meter;
    private readonly Counter<long> _pairing;
    private readonly Counter<long> _connect;

    /// <summary>Initializes a new instance of the <see cref="DispatchMetrics"/> class.</summary>
    /// <param name="meterFactory">The meter factory.</param>
    public DispatchMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(ApiTelemetry.SourceName);
        _pairing = _meter.CreateCounter<long>(PairingName, description: "Dispatch pairing attempts. Tags: result, reason.");
        _connect = _meter.CreateCounter<long>(ConnectName, description: "Dispatch connect attempts. Tags: result, reason.");
    }

    /// <summary>Records one pairing outcome.</summary>
    /// <param name="result">The outcome, such as <c>created</c> or <c>refused</c>.</param>
    /// <param name="reason">The stable reason. Never a secret.</param>
    public void Pairing(string result, string reason) => _pairing.Add(1, Tag(result), TagReason(reason));

    /// <summary>Records one connect outcome.</summary>
    /// <param name="result">The outcome.</param>
    /// <param name="reason">The stable reason. Never a secret.</param>
    public void Connect(string result, string reason) => _connect.Add(1, Tag(result), TagReason(reason));

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();

    private static KeyValuePair<string, object?> Tag(string result) => new("result", result);

    private static KeyValuePair<string, object?> TagReason(string reason) => new("reason", reason);
}
