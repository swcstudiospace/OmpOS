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
/// Endpoint <c>https://backboard.railway.com/graphql/v2</c> and Bearer authentication are verified
/// against docs.railway.com/integrations/api. The schema was introspected for Q-SB-RAILWAY:
/// <c>Mutation.serviceDelete(environmentId: String, id: String!): Boolean!</c> is the only operation
/// this provider calls. Provisioning, exec, logs and snapshot each name a schema operation, but none
/// can be called with the inputs the abstraction supplies, so each fails closed with a
/// <see cref="NotAvailableException"/> that names the blocking fact. Attach has no schema operation
/// and fails closed the same way.
/// </summary>
public sealed class RailwaySwarmProvider : ISandboxProvider
{
    private const string ProvisionReason =
        "sandboxCreate(input: SandboxCreateInput!) is in the schema but cannot be called: no Railway environmentId is configured, SandboxCreateInput has no image or command field, and no verified default-deny egress policy maps to NetworkPolicy.DefaultDeny (Q-SB-RAILWAY)";
    private const string AttachReason =
        "no Railway attach operation exists in the introspected schema; Query.sandbox(environmentId, id) needs an environmentId that is not configured (Q-SB-RAILWAY)";
    private const string ExecReason =
        "sandboxExec(command: String!, environmentId: String!, id: String!, timeoutSec: Int) is in the schema but cannot be called: no Railway environmentId is configured, and ExecRequest argv, working directory, environment and TTY have no verified mapping to its single command string (Q-SB-RAILWAY)";
    private const string LogsReason =
        "deploymentLogs(deploymentId: String!, ...) is in the schema but cannot be called: the Sandbox type exposes no deployment identifier to map sandboxId to, and LogCursor has no verified mapping to its startDate or limit arguments (Q-SB-RAILWAY)";
    private const string SnapshotReason =
        "sandboxCheckpointCreate(environmentId: String!, name: String!, sandboxId: String!) is in the schema but cannot be called: no Railway environmentId is configured, and it returns a checkpoint reference (id, key), not the snapshot bytes SnapshotResult requires (Q-SB-RAILWAY)";
    private const string CredentialReason =
        "per-sandbox credentials cannot be issued: no verified Railway provisioning path exists because sandboxCreate cannot be called with the configured inputs, and SandboxCreateInput has no secret field (Q-SB-RAILWAY)";
    private const string ServiceDeleteQuery = "mutation serviceDelete($id: String!) { serviceDelete(id: $id) }";
    private const string RedactedToken = "[REDACTED]";

    private static readonly Uri GraphQlEndpoint = new("https://backboard.railway.com/graphql/v2");

    private readonly (HttpClient Client, string Token)? _account;
    private readonly bool _perSandboxCredentialsRequested;

    /// <summary>
    /// Initializes an unconfigured provider. Teardown is a no-op because this provider has no
    /// Railway credentials and therefore cannot have created a service.
    /// </summary>
    /// <param name="options">Optional. When set with PerSandboxCredentials true, provisioning fails closed before any request. Null or PerSandboxCredentials false leaves provisioning unchanged.</param>
    public RailwaySwarmProvider(SandboxProviderOptions? options = null)
    {
        _perSandboxCredentialsRequested = options?.PerSandboxCredentials ?? false;
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
    /// <param name="options">Optional. When set with PerSandboxCredentials true, provisioning fails closed before any request. Null or PerSandboxCredentials false leaves provisioning unchanged.</param>
    public RailwaySwarmProvider(HttpClient httpClient, string apiToken, SandboxProviderOptions? options = null)
        : this(options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiToken);
        _account = (httpClient, apiToken);
    }

    /// <inheritdoc />
    public SandboxCapability Capabilities { get; }

    /// <inheritdoc />
    /// <exception cref="NotAvailableException">Always. The schema operation cannot be called with the request's inputs, and no per-sandbox credential can be issued.</exception>
    public Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(_perSandboxCredentialsRequested ? CredentialReason : ProvisionReason);
    }

    /// <inheritdoc />
    /// <exception cref="NotAvailableException">Always. No Railway attach operation exists in the schema.</exception>
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(AttachReason);
    }

    /// <inheritdoc />
    /// <exception cref="NotAvailableException">Always. The <c>sandboxExec</c> inputs cannot be mapped from the request.</exception>
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(ExecReason);
    }

    /// <inheritdoc />
    /// <exception cref="NotAvailableException">Always. The sandbox cannot be mapped to a deployment for <c>deploymentLogs</c>.</exception>
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(LogsReason);
    }

    /// <inheritdoc />
    /// <exception cref="NotAvailableException">Always. <c>sandboxCheckpointCreate</c> returns a reference, not snapshot bytes.</exception>
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(SnapshotReason);
    }

    /// <summary>
    /// Deletes the Railway service named by <paramref name="sandboxId"/> through the
    /// <c>serviceDelete</c> mutation. The schema declares its return type as <c>Boolean!</c>, so
    /// success is <c>data.serviceDelete</c> equal to <c>true</c>. An unconfigured provider completes
    /// without a request. A service that no longer exists is reported as a
    /// <see cref="TeardownFailedException"/>: the public API documents no not-found error code, so
    /// NotFound-as-success is not verified.
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
            payload = await BoundedContent.ReadTextAsync(response.Content, BoundedContent.MaxVendorBodyBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TeardownFailedException(sandboxId, 1, "Railway serviceDelete timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new TeardownFailedException(sandboxId, 1, "Railway serviceDelete request failed", ex);
        }
        catch (InvalidDataException ex)
        {
            throw new TeardownFailedException(sandboxId, 1, "Railway serviceDelete returned a body over the 4 MiB limit", ex);
        }

        EnsureServiceDeleted(sandboxId, token, statusCode, payload);
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

    private static void EnsureServiceDeleted(string sandboxId, string token, int statusCode, string payload)
    {
        if (statusCode is < 200 or >= 300)
        {
            throw new TeardownFailedException(sandboxId, 1, $"Railway serviceDelete returned HTTP {statusCode}");
        }

        var document = ParseDocument(sandboxId, payload);
        if (document["errors"] is JsonArray { Count: > 0 } errors)
        {
            var message = Redact(FirstErrorMessage(errors), token);
            throw new TeardownFailedException(sandboxId, 1, $"Railway serviceDelete GraphQL error: {message}");
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

    // Server-supplied text is untrusted: remove any echo of the credential before it reaches an exception.
    private static string Redact(string text, string token) =>
        text.Replace(token, RedactedToken, StringComparison.Ordinal);
}
