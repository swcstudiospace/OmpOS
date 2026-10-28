using System.Diagnostics.Metrics;
using OmpOS.Api.Telemetry;

namespace OmpOS.Api.Cloud;

/// <summary>
/// The cloud agent instruments, on the API's meter (<see cref="OmpTelemetry.SourceName"/>), so the existing export reaches them. The compute-second
/// counter is the cost signal: it adds the awake seconds of every agent when an awake stretch ends. Tags never carry an owner, a token or a secret.
/// </summary>
internal sealed class CloudMetrics : IDisposable
{
    /// <summary>The instrument name of the awake-seconds counter.</summary>
    public const string ComputeSecondsName = "ompos.cloud.compute.seconds";

    /// <summary>The instrument name of the created-agents counter.</summary>
    public const string AgentsCreatedName = "ompos.cloud.agents.created";

    /// <summary>The instrument name of the refused-by-cap counter.</summary>
    public const string LimitRefusedName = "ompos.cloud.agents.limit_refused";

    /// <summary>The instrument name of the idle-transitions counter.</summary>
    public const string IdleTransitionsName = "ompos.cloud.agents.idle";

    /// <summary>The instrument name of the failed-provisioning counter.</summary>
    public const string ProvisionFailuresName = "ompos.cloud.provision.failures";

    private readonly Meter _meter;
    private readonly Counter<double> _computeSeconds;
    private readonly Counter<long> _created;
    private readonly Counter<long> _limitRefused;
    private readonly Counter<long> _idle;
    private readonly Counter<long> _provisionFailures;
    private readonly Histogram<double> _provisionMs;

    /// <summary>Initializes a new instance of the <see cref="CloudMetrics"/> class.</summary>
    /// <param name="meterFactory">The meter factory.</param>
    public CloudMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(OmpTelemetry.SourceName);
        _computeSeconds = _meter.CreateCounter<double>(ComputeSecondsName, unit: "s", description: "Seconds cloud agent workers were awake.");
        _created = _meter.CreateCounter<long>(AgentsCreatedName, description: "Cloud agents created.");
        _limitRefused = _meter.CreateCounter<long>(LimitRefusedName, description: "Cloud agent creations or wakes refused by a cap. Tag: reason.");
        _idle = _meter.CreateCounter<long>(IdleTransitionsName, description: "Cloud agents put to idle by the sweeper or by a stop. Tag: cause.");
        _provisionFailures = _meter.CreateCounter<long>(ProvisionFailuresName, description: "Cloud agent provisioning or wake failures. Tag: code.");
        _provisionMs = _meter.CreateHistogram<double>("ompos.cloud.provision.time", unit: "ms", description: "Time from creation to a running worker.");
    }

    /// <summary>Records one created agent.</summary>
    public void AgentCreated() => _created.Add(1);

    /// <summary>Records one refusal by a cap.</summary>
    /// <param name="reason">The cap: <c>agents</c>, <c>running</c> or <c>compute</c>.</param>
    public void LimitRefused(string reason) => _limitRefused.Add(1, new KeyValuePair<string, object?>("reason", reason));

    /// <summary>Adds the seconds of a finished awake stretch.</summary>
    /// <param name="seconds">The seconds; non-positive values are ignored.</param>
    public void ComputeSeconds(double seconds)
    {
        if (seconds > 0)
        {
            _computeSeconds.Add(seconds);
        }
    }

    /// <summary>Records one agent put to idle.</summary>
    /// <param name="cause">Why: <c>sweep</c>, <c>stop</c> or <c>limit</c>.</param>
    public void WentIdle(string cause) => _idle.Add(1, new KeyValuePair<string, object?>("cause", cause));

    /// <summary>Records one provisioning or wake failure.</summary>
    /// <param name="code">The stable failure code.</param>
    public void ProvisionFailed(string code) => _provisionFailures.Add(1, new KeyValuePair<string, object?>("code", code));

    /// <summary>Records the time one agent took to become ready.</summary>
    /// <param name="milliseconds">The duration.</param>
    public void Provisioned(double milliseconds) => _provisionMs.Record(milliseconds);

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
