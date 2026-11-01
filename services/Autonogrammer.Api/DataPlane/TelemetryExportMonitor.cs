using Autonogrammer.Api.Options;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Tells the status service whether the OTLP exporter works. Two sources: an <see cref="ExporterEventListener"/> on the exporter's event sources (a warning or an
/// error in the last <see cref="FailureWindow"/> means failing) and, when the exporter protocol is <c>http/protobuf</c>, a <c>GET /health</c> probe of the OTLP host
/// every 30 seconds with a 2 second timeout (an answer other than 2xx, a refusal or a timeout means failing). A warning ages out after the window. The monitor
/// does nothing unless <c>Otel:Exporter</c> is <c>otlp</c>. It stops on the stopping token, well inside <c>Hosting:ShutdownTimeoutSeconds</c>.
/// </summary>
internal sealed partial class TelemetryExportMonitor : BackgroundService
{
    /// <summary>Gets how long an exporter warning keeps the exporter reported as failing.</summary>
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(2);

    /// <summary>Gets the interval of the health probe.</summary>
    public static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);

    /// <summary>Gets the timeout of one health probe.</summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    private const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string ProtocolKey = "OTEL_EXPORTER_OTLP_PROTOCOL";
    private const string HttpProtobuf = "http/protobuf";

    private readonly IOptions<OtelOptions> _otel;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _time;
    private readonly ILogger<TelemetryExportMonitor> _logger;
    private readonly Func<Uri, CancellationToken, Task<bool>> _probe;
    private ExporterEventListener? _listener;
    private int _probeFailed;

    /// <summary>Initializes a new instance of the <see cref="TelemetryExportMonitor"/> class.</summary>
    /// <param name="otel">The telemetry options.</param>
    /// <param name="configuration">The configuration that holds the standard OTLP variables.</param>
    /// <param name="time">The clock of the probe interval.</param>
    /// <param name="logger">The logger.</param>
    public TelemetryExportMonitor(IOptions<OtelOptions> otel, IConfiguration configuration, TimeProvider time, ILogger<TelemetryExportMonitor> logger)
        : this(otel, configuration, time, logger, ProbeHealthAsync)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TelemetryExportMonitor"/> class with an explicit probe.</summary>
    /// <param name="otel">The telemetry options.</param>
    /// <param name="configuration">The configuration that holds the standard OTLP variables.</param>
    /// <param name="time">The clock of the probe interval.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="probe">Answers whether the health address replied with a success status.</param>
    internal TelemetryExportMonitor(IOptions<OtelOptions> otel, IConfiguration configuration, TimeProvider time, ILogger<TelemetryExportMonitor> logger, Func<Uri, CancellationToken, Task<bool>> probe)
    {
        _otel = otel;
        _configuration = configuration;
        _time = time;
        _logger = logger;
        _probe = probe;
    }

    /// <summary>Gets a value indicating whether the exporter is on at all (<c>Otel:Exporter</c> is <c>otlp</c>).</summary>
    public bool IsExporting => string.Equals(_otel.Value.Exporter, "otlp", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets a value indicating whether the exporter reported a failure recently or the last health probe failed.</summary>
    public bool IsFailing => IsExporting && (Volatile.Read(ref _probeFailed) == 1 || (_listener?.FailedWithin(FailureWindow) ?? false));

    /// <summary>Gets the number of exporter warning and error events seen, for tests.</summary>
    internal long FailureEvents => _listener?.FailureEvents ?? 0;

    /// <inheritdoc />
    public override void Dispose()
    {
        _listener?.Dispose();
        base.Dispose();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsExporting)
        {
            return;
        }

        _listener = new ExporterEventListener();
        var health = HealthAddress();
        if (health is null)
        {
            return;
        }

        using var timer = new PeriodicTimer(ProbeInterval, _time);
        try
        {
            do
            {
                var ok = await ProbeOnceAsync(health, stoppingToken).ConfigureAwait(false);
                _ = Interlocked.Exchange(ref _probeFailed, ok ? 0 : 1);
                if (!ok)
                {
                    LogProbeFailed(_logger);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private static async Task<bool> ProbeHealthAsync(Uri address, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = ProbeTimeout };
        using var response = await client.GetAsync(address, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> ProbeOnceAsync(Uri address, CancellationToken stoppingToken)
    {
        try
        {
            return await _probe(address, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private Uri? HealthAddress()
    {
        var protocol = _configuration[ProtocolKey];
        if (!string.Equals(protocol, HttpProtobuf, StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(_configuration[EndpointKey], UriKind.Absolute, out var endpoint)
            || (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            return null;
        }

        return new Uri(endpoint.GetLeftPart(UriPartial.Authority) + "/health", UriKind.Absolute);
    }

    [LoggerMessage(EventId = 6851, Level = LogLevel.Warning, Message = "The health probe of the OTLP endpoint failed; telemetry export is reported as failing.")]
    private static partial void LogProbeFailed(ILogger logger);
}
