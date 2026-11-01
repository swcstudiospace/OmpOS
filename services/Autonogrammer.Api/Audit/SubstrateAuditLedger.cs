using System.Security.Cryptography;
using System.Text.Json;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Harness.AgentLoop.Substrate;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.Audit;

/// <summary>
/// The ledger sink when <c>DataPlane:Ledger:Enabled</c> is true. The request path only enqueues. The only write to Substrate is
/// <see cref="SubstrateGateway.EmitEventAsync"/>, called by the pump, under the gateway's endpoint rules. The bearer token is read per request and never logged.
/// </summary>
internal sealed class SubstrateAuditLedger : IAuditLedger, IDisposable
{
    private readonly IOptionsMonitor<DataPlaneOptions> _options;
    private readonly ILedgerOutboxStore _outbox;
    private readonly SubstrateGateway _gateway;
    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="SubstrateAuditLedger"/> class.</summary>
    /// <param name="options">The data-plane options. <c>Ledger:Endpoint</c> and <c>Ledger:Token</c> are read from here.</param>
    /// <param name="outbox">Where the notarisation copy is queued.</param>
    /// <param name="timeProvider">The clock passed to the gateway.</param>
    /// <param name="handler">A test handler. Production passes null and uses <see cref="SubstrateGateway.CreateHttpClient"/>.</param>
    public SubstrateAuditLedger(
        IOptionsMonitor<DataPlaneOptions> options,
        ILedgerOutboxStore outbox,
        TimeProvider timeProvider,
        HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _options = options;
        _outbox = outbox;
        var endpoint = options.CurrentValue.Ledger.Endpoint ?? throw new InvalidOperationException("DataPlane:Ledger:Endpoint is not set.");
        _httpClient = handler is null
            ? SubstrateGateway.CreateHttpClient()
            : new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) };
        _gateway = new SubstrateGateway(_httpClient, new SubstrateEndpoint(new Uri(endpoint)), Token, timeProvider);
    }

    /// <inheritdoc />
    public ValueTask RecordAsync(AuditLedgerEvent audit, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        return _outbox.EnqueueAsync(audit, transaction, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<bool> NotariseAsync(AuditLedgerEvent audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        var result = await _gateway.EmitEventAsync(ToEmit(audit), cancellationToken).ConfigureAwait(false);
        return result.Stored;
    }

    /// <inheritdoc />
    public void Dispose() => _httpClient.Dispose();

    /// <summary>Builds the digest stored in <c>ledger_outbox.payload_digest</c>. It covers the closed field set and nothing else.</summary>
    /// <param name="audit">The event.</param>
    /// <returns>Lowercase hex SHA-256 of the canonical JSON.</returns>
    internal static string Digest(AuditLedgerEvent audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(audit, AuditJsonContext.Default.AuditLedgerEvent);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    internal static SubstrateEventEmit ToEmit(AuditLedgerEvent audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        return new SubstrateEventEmit
        {
            Kind = AuditLedgerEvent.Kind,
            Summary = audit.Summary,
            GraphId = audit.TenantHash,
            Payload = new SubstrateEventPayload
            {
                Action = audit.Action,
                Item = audit.Item,
                Outcome = audit.Outcome,
            },
        };
    }

    private string? Token() => _options.CurrentValue.Ledger.Token;
}
