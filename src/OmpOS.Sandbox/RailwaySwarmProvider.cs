using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;

namespace OmpOS.Sandbox;

/// <summary>
/// Railway sandbox provider over the Railway public GraphQL API.
/// Verified against docs.railway.com/integrations/api: endpoint
/// <c>https://backboard.railway.com/graphql/v2</c>, Bearer authentication for account and
/// workspace tokens, and the <c>serviceDelete</c> mutation. Only teardown is implemented.
/// Provisioning is refused because Railway services offer no egress control, so the
/// default-deny outbound policy (SBOX-05) cannot be enforced. Attach, exec, logs and snapshot
/// have no verified API contract. Open question Q-SB-RAILWAY stays open with this blocker.
/// </summary>
public sealed class RailwaySwarmProvider : ISandboxProvider
{
    private const string EgressReason =
        "Railway services cannot enforce default-deny outbound networking (SBOX-05); the public API exposes no egress control (Q-SB-RAILWAY)";
    private const string AttachReason = "Railway public API documents no attach operation (Q-SB-RAILWAY)";
    private const string ExecReason = "Railway public API documents no exec operation (Q-SB-RAILWAY)";
    private const string LogsReason = "Railway log streaming is documented only as WebSocket subscriptions and is unverified (Q-SB-RAILWAY)";
    private const string SnapshotReason = "Railway public API documents no sandbox snapshot operation (Q-SB-RAILWAY)";
    private const string ServiceDeleteQuery = "mutation serviceDelete($id: String!) { serviceDelete(id: $id) }";

    private static readonly Uri GraphQlEndpoint = new("https://backboard.railway.com/graphql/v2");

    private readonly (HttpClient Client, string Token)? _account;

    /// <summary>
    /// Initializes an unconfigured provider. Teardown is a no-op because this provider has no
    /// Railway credentials and therefore cannot have created a service.
    /// </summary>
    public RailwaySwarmProvider()
    {
        Capabilities = new SandboxCapability
        {
            Isolation = Isolation.Container,
            Attestation = Attestation.None, // Railway offers no attestation
            NativeBinaries = false,
            SnapshotsSupported = false,
            MaxTtlSeconds = 86400,
        };
    }

    /// <summary>
    /// Initializes a provider that sends teardown requests to the Railway public API.
    /// </summary>
    /// <param name="httpClient">The client used for Railway requests. Its lifetime belongs to the caller.</param>
    /// <param name="apiToken">An account or workspace API token, sent as a Bearer token.</param>
    public RailwaySwarmProvider(HttpClient httpClient, string apiToken)
        : this()
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiToken);
        _account = (httpClient, apiToken);
    }

    /// <inheritdoc />
    public SandboxCapability Capabilities { get; }

    /// <inheritdoc />
    /// <exception cref="NotAvailableException">Always. Default-deny egress cannot be enforced on Railway.</exception>
    public Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(EgressReason);
    }

    /// <inheritdoc />
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(AttachReason);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(ExecReason);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(LogsReason);
    }

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(SnapshotReason);
    }

    /// <summary>
    /// Deletes the Railway service named by <paramref name="sandboxId"/> through the
    /// <c>serviceDelete</c> mutation. An unconfigured provider completes without a request.
    /// A service that no longer exists is reported as a <see cref="TeardownFailedException"/>:
    /// the public API documents no not-found error code, so NotFound-as-success is not verified.
    /// </summary>
    /// <param name="sandboxId">The Railway service identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TeardownFailedException">Thrown when Railway rejects, fails or does not confirm the deletion.</exception>
    public Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxId);
        return _account is { } account
            ? DeleteServiceAsync(account.Client, account.Token, sandboxId, cancellationToken)
            : Task.CompletedTask;
    }

    private static async Task DeleteServiceAsync(HttpClient client, string token, string sandboxId, CancellationToken cancellationToken)
    {
        using var request = BuildServiceDeleteRequest(token, sandboxId);
        int statusCode;
        string payload;
        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            statusCode = (int)response.StatusCode;
            payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TeardownFailedException(sandboxId, 1, "Railway serviceDelete timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new TeardownFailedException(sandboxId, 1, "Railway serviceDelete request failed", ex);
        }

        EnsureServiceDeleted(sandboxId, statusCode, payload);
    }

    private static HttpRequestMessage BuildServiceDeleteRequest(string token, string sandboxId)
    {
        var body = new JsonObject
        {
            ["query"] = ServiceDeleteQuery,
            ["variables"] = new JsonObject { ["id"] = sandboxId },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static void EnsureServiceDeleted(string sandboxId, int statusCode, string payload)
    {
        if (statusCode is < 200 or >= 300)
        {
            throw new TeardownFailedException(sandboxId, 1, $"Railway serviceDelete returned HTTP {statusCode}");
        }

        var document = ParseDocument(sandboxId, payload);
        if (document["errors"] is JsonArray { Count: > 0 } errors)
        {
            throw new TeardownFailedException(sandboxId, 1, $"Railway serviceDelete GraphQL error: {FirstErrorMessage(errors)}");
        }

        if (document["data"] is JsonObject data
            && data["serviceDelete"] is JsonValue value
            && value.TryGetValue(out bool deleted)
            && deleted)
        {
            return;
        }

        throw new TeardownFailedException(sandboxId, 1, "Railway serviceDelete did not confirm deletion");
    }

    private static JsonObject ParseDocument(string sandboxId, string payload)
    {
        JsonObject? document;
        try
        {
            document = JsonNode.Parse(payload) as JsonObject;
        }
        catch (JsonException ex)
        {
            throw new TeardownFailedException(sandboxId, 1, "Railway serviceDelete returned a malformed body", ex);
        }

        return document ?? throw new TeardownFailedException(sandboxId, 1, "Railway serviceDelete returned a non-object body");
    }

    private static string FirstErrorMessage(JsonArray errors)
    {
        string? text = null;
        if (errors[0] is JsonObject first && first["message"] is JsonValue message)
        {
            message.TryGetValue(out text);
        }

        return text ?? "no message";
    }
}
