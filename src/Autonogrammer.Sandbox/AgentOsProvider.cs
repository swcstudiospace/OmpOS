using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;

namespace Autonogrammer.Sandbox;

/// <summary>
/// agentOS sandbox provider over the hosted agentOS HTTP API (https://rivet.dev/agentos/docs/hosted-api/).
/// Verified calls: <c>PUT /actors?namespace=</c> (create or get a VM by key), <c>DELETE /actors/{id}?namespace=</c>
/// (destroy; 404 means already gone), and <c>POST /gateway/{id}/action/v1.{group}.{action}</c> for
/// <c>v1.vm.status</c>, <c>v1.config.set</c> (network permission <c>deny</c>), <c>v1.config.patch</c> (guest environment)
/// and <c>v1.process.run</c> (argv with captured stdio). <c>ExecRequest.Timeout</c> bounds the client HTTP call only;
/// the documented process.run input has no timeout field.
/// A request field without a verified mapping fails with <see cref="NotAvailableException"/> before any call is sent.
/// With PerSandboxCredentials set, provisioning fails closed before any request: the hosted provisioning request has no documented secrets field.
/// Open question: Q-SB-AGENTOS.
/// </summary>
public sealed class AgentOsProvider : ISandboxProvider
{
    private const string ActorName = "agentOS";
    private const string RunnerNameSelector = "services";
    private const string CrashPolicy = "sleep";
    private const string TokenHeader = "x-rivet-token";
    private const string StatusAction = "v1.vm.status";
    private const string ConfigPatchAction = "v1.config.patch";
    private const string ConfigSetAction = "v1.config.set";
    private const string DenyNetwork = "deny";
    private const string RunAction = "v1.process.run";
    private const string SnapshotNotSupported = "agentOS hosted API has no VM snapshot action; filesystem.export is a root-filesystem snapshot whose bytes the hosted JSON encoding cannot carry (Q-SB-AGENTOS)";
    private const string LogsNotMapped = "agentOS output.read returns process output as bytes; the hosted JSON encoding cannot carry raw bytes and no other encoding is documented (Q-SB-AGENTOS)";
    private static readonly TimeSpan ControlTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _namespace;
    private readonly string _secretToken;
    private readonly bool _perSandboxCredentialsRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentOsProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client used for hosted API calls.</param>
    /// <param name="endpoint">
    /// Hosted API base URL, for example <c>https://api.rivet.dev</c>. It must be https, or http only for a loopback host
    /// (<c>localhost</c>, <c>127.0.0.0/8</c>, <c>::1</c>); any other cleartext origin is refused at construction.
    /// </param>
    /// <param name="namespaceName">Rivet namespace that owns the VMs.</param>
    /// <param name="secretToken">Namespace secret token (<c>sk_</c>), sent as bearer for control-plane calls and as <c>x-rivet-token</c> for actions. It is sent only to the configured origin.</param>
    /// <param name="options">Optional. When set with PerSandboxCredentials true, provisioning fails closed before any request. Null or PerSandboxCredentials false leaves provisioning unchanged.</param>
    public AgentOsProvider(HttpClient httpClient, Uri endpoint, string namespaceName, string secretToken, SandboxProviderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretToken);
        if (!endpoint.IsAbsoluteUri)
        {
            throw new ArgumentException("Endpoint must be an absolute URI.", nameof(endpoint));
        }

        RequireTrustedOrigin(endpoint);

        _http = httpClient;
        _endpoint = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
        _namespace = namespaceName;
        _secretToken = secretToken;
        _perSandboxCredentialsRequested = options?.PerSandboxCredentials ?? false;
        Capabilities = new SandboxCapability
        {
            Isolation = Isolation.Process,
            Attestation = Attestation.None,
            NativeBinaries = false,
            SnapshotsSupported = false,
            MaxTtlSeconds = 86400,
        };
    }

    /// <inheritdoc />
    public SandboxCapability Capabilities { get; }

    /// <summary>
    /// Creates an HTTP client that never follows redirects, so the bearer token and the <c>x-rivet-token</c> header cannot
    /// be replayed to another host by a redirect. Use it for the <c>httpClient</c> constructor argument in production.
    /// A client from another source is still checked: a response that was served from a different origin is refused.
    /// </summary>
    /// <returns>A new client the caller owns and disposes.</returns>
    public static HttpClient CreateHttpClient()
    {
        SocketsHttpHandler? handler = new();
        try
        {
            handler.AllowAutoRedirect = false;
            var client = new HttpClient(handler, disposeHandler: true);
            handler = null;
            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireCredentialFailClosed();
        RequireProvisionMapped(request);
        try
        {
            return await CreateSandboxAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            throw new ProvisionTimeoutException("agentOS provisioning timed out", ex);
        }
    }

    /// <inheritdoc />
    public async Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxId);
        try
        {
            await ActionAsync(sandboxId, StatusAction, null, ControlTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            throw new NotAvailableException("agentOS attach timed out", ex);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxId);
        ArgumentNullException.ThrowIfNull(request);
        RequireExecMapped(request);
        return RunExecAsync(sandboxId, request, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(LogsNotMapped);
    }

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxId);
        return Task.FromResult(SnapshotResult.NotSupportedResult(SnapshotNotSupported));
    }

    /// <inheritdoc />
    public async Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxId);
        try
        {
            var uri = ControlPlaneUri($"actors/{Uri.EscapeDataString(sandboxId)}");
            using var response = await SendAsync(HttpMethod.Delete, uri, null, gateway: false, ControlTimeout, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            throw new TeardownFailedException(sandboxId, 1, $"agentOS destroy failed: HTTP {(int)response.StatusCode}");
        }
        catch (TimeoutException ex)
        {
            throw new TeardownFailedException(sandboxId, 1, "agentOS destroy timed out", ex);
        }
    }

    private async IAsyncEnumerable<ExecEvent> RunExecAsync(string sandboxId, ExecRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var input = new JsonObject
        {
            ["command"] = request.Argv[0],
            ["args"] = ToJsonArray(request.Argv.Skip(1)),
            ["options"] = new JsonObject { ["captureStdio"] = true },
        };
        var output = await ActionAsync(sandboxId, RunAction, input, request.Timeout, cancellationToken).ConfigureAwait(false);
        var exitCode = ReadInt(Member(Member(output, "status"), "exitCode"))
            ?? throw new NotAvailableException("agentOS process.run returned no exit code");
        var stdout = ReadString(Member(output, "stdout"));
        if (!string.IsNullOrEmpty(stdout))
        {
            yield return new ExecEvent { Type = ExecEventType.Stdout, Stdout = stdout };
        }

        var stderr = ReadString(Member(output, "stderr"));
        if (!string.IsNullOrEmpty(stderr))
        {
            yield return new ExecEvent { Type = ExecEventType.Stderr, Stderr = stderr };
        }

        yield return new ExecEvent { Type = ExecEventType.Exit, ExitCode = exitCode };
    }

    private async Task<ProvisionResult> CreateSandboxAsync(ProvisionRequest request, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["name"] = ActorName,
            ["key"] = request.IdempotencyKey,
            ["runner_name_selector"] = RunnerNameSelector,
            ["crash_policy"] = CrashPolicy,
        };
        using var response = await SendAsync(HttpMethod.Put, ControlPlaneUri("actors"), body, gateway: false, request.Timeout, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw Rejected("provisioning", response.StatusCode);
        }

        var json = await ReadJsonAsync(response, "provisioning", cancellationToken).ConfigureAwait(false);
        var actorId = ReadString(Member(Member(json, "actor"), "actor_id"))
            ?? throw new NotAvailableException("agentOS provisioning response has no actor_id");
        var created = ReadBool(Member(json, "created"))
            ?? throw new NotAvailableException("agentOS provisioning response has no created flag");
        // config.set replaces the complete config, so it runs before the environment patch.
        await DenyNetworkAsync(actorId, request.Timeout, cancellationToken).ConfigureAwait(false);

        if (request.Spec.Environment.Count > 0)
        {
            await PatchEnvironmentAsync(actorId, request.Spec.Environment, request.Timeout, cancellationToken).ConfigureAwait(false);
        }

        return BuildProvisionResult(request, actorId, created);
    }

    private async Task PatchEnvironmentAsync(string actorId, IReadOnlyDictionary<string, string> environment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var variables = new JsonObject();
        foreach (var (name, value) in environment)
        {
            variables[name] = value;
        }

        var input = new JsonObject { ["patch"] = new JsonObject { ["environment"] = variables } };
        await ActionAsync(actorId, ConfigPatchAction, input, timeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task DenyNetworkAsync(string actorId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var input = new JsonObject
        {
            ["config"] = new JsonObject { ["permissions"] = new JsonObject { ["network"] = DenyNetwork } },
        };
        await ActionAsync(actorId, ConfigSetAction, input, timeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonNode?> ActionAsync(string sandboxId, string action, JsonNode? input, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var args = input is null ? new JsonArray() : new JsonArray(input);
        var body = new JsonObject { ["args"] = args };
        using var response = await SendAsync(HttpMethod.Post, ActionUri(sandboxId, action), body, gateway: true, timeout, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw Rejected(action, response.StatusCode);
        }

        var json = await ReadJsonAsync(response, action, cancellationToken).ConfigureAwait(false);
        return Member(json, "output");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri uri, JsonNode? body, bool gateway, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (gateway)
        {
            request.Headers.Add(TokenHeader, _secretToken);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretToken);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, deadline.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new NotAvailableException("agentOS hosted API is unreachable", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("agentOS request exceeded its deadline", ex);
        }

        var servedFrom = response.RequestMessage?.RequestUri;
        var redirected = (int)response.StatusCode is >= 300 and < 400;
        if (redirected || (servedFrom is not null && !IsConfiguredOrigin(servedFrom)))
        {
            response.Dispose();
            throw new NotAvailableException("agentOS hosted API answered with a redirect or from another origin; the request is refused so the token stays on the configured origin");
        }

        return response;
    }

    private bool IsConfiguredOrigin(Uri uri) =>
        string.Equals(uri.GetLeftPart(UriPartial.Authority), _endpoint.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);

    private static void RequireTrustedOrigin(Uri endpoint)
    {
        if (string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && endpoint.IsLoopback)
        {
            return;
        }

        throw new ArgumentException("Endpoint must be https, or http on a loopback host; a bearer token is never sent over cleartext to another host.", nameof(endpoint));
    }

    private Uri ControlPlaneUri(string path) => new(_endpoint, $"{path}?namespace={Uri.EscapeDataString(_namespace)}");

    private Uri ActionUri(string actorId, string action) => new(_endpoint, $"gateway/{Uri.EscapeDataString(actorId)}/action/{action}");

    private static ProvisionResult BuildProvisionResult(ProvisionRequest request, string actorId, bool created) => new()
    {
        Descriptor = new SandboxDescriptor
        {
            Id = actorId,
            SessionId = request.SessionId,
            Isolation = Isolation.Process,
            Attestation = Attestation.None,
            NativeBinaries = false,
            TtlSeconds = (long)request.Timeout.TotalSeconds,
            Labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["autonogrammer.session"] = request.SessionId,
                ["autonogrammer.idempotency"] = request.IdempotencyKey,
            },
        },
        NewlyProvisioned = created,
    };

    private static void RequireProvisionMapped(ProvisionRequest request)
    {
        if (request.Timeout <= TimeSpan.Zero)
        {
            throw new ProvisionTimeoutException("Timeout must be positive");
        }

        if (request.Isolation != Isolation.Process)
        {
            throw Unmapped("isolation other than process", "hosted VMs are process-isolated and no isolation selector is documented");
        }

        if (request.NetworkPolicy.OutboundDefault != OutboundDefault.Deny)
        {
            throw Unmapped("allow-by-default outbound policy", "the documented default is the sidecar allowlist and no allow-all equivalent is documented");
        }

        if (request.NetworkPolicy.Rules.Count > 0)
        {
            throw Unmapped("outbound network rules", "NetworkRule (destination, port, protocol, action) has no documented permission pattern or operation mapping");
        }

        if (request.Mounts.Count > 0)
        {
            throw Unmapped("mounts", "hosted filesystem.mounts accepts only the registry backend {type: durable}; host bind paths are rejected and no read-only field is documented");
        }

        if (request.SecretRefs.Count > 0)
        {
            throw Unmapped("secret references", "no secret field is documented for hosted config; config.environment is the only documented variable channel");
        }

        if (request.Spec.Image.Length > 0)
        {
            throw Unmapped("image selection", "hosted VMs are prebuilt agentOS actors and no image field is documented");
        }

        if (request.Spec.Command.Count > 0)
        {
            throw Unmapped("provisioning command", "no create-time command field is documented; process.spawn has no documented sandbox lifetime");
        }

        if (request.Spec.WorkingDirectory.Length > 0)
        {
            throw Unmapped("provisioning working directory", "no create-time working directory field is documented");
        }

        if (request.Spec.Limits is not null)
        {
            throw Unmapped("resource limits", "limits.resources.maxProcesses has no documented relation to a pids limit, and no memory-bytes or CPU-core field is documented");
        }
    }

    private void RequireCredentialFailClosed()
    {
        if (_perSandboxCredentialsRequested)
        {
            throw Unmapped("per-sandbox credentials", "no secrets field is documented on provisioning, and config.environment is VM-wide so it cannot carry a per-sandbox credential");
        }
    }

    private static void RequireExecMapped(ExecRequest request)
    {
        if (request.Argv.Count == 0 || request.Argv[0].Length == 0)
        {
            throw new ArgumentException("Argv must start with a command name.", nameof(request));
        }

        if (request.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Timeout must be positive.", nameof(request));
        }

        if (request.WorkingDirectory.Length > 0)
        {
            throw Unmapped("per-exec working directory", "v1.process.run input is command, args and options.captureStdio; no cwd field is documented");
        }

        if (request.Environment.Count > 0)
        {
            throw Unmapped("per-exec environment", "v1.process.run has no documented env field; environment is VM-wide through config.environment");
        }

        if (request.Tty)
        {
            throw Unmapped("TTY allocation", "v1.process.run has no PTY option; PTY is documented only for v1.terminal.open");
        }
    }

    private static NotAvailableException Unmapped(string field, string missingFact) =>
        new($"agentOS hosted API has no verified mapping for {field}: {missingFact} (Q-SB-AGENTOS)");

    private static NotAvailableException Rejected(string operation, HttpStatusCode status) =>
        new($"agentOS {operation} failed: HTTP {(int)status}");

    private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await BoundedContent.ReadTextAsync(response.Content, BoundedContent.MaxVendorBodyBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            throw new NotAvailableException($"agentOS {operation} returned a body over the 4 MiB limit", ex);
        }
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new NotAvailableException($"agentOS {operation} returned a non-JSON body", ex);
        }
    }

    private static JsonNode? Member(JsonNode? node, string name) =>
        node is JsonObject obj && obj.TryGetPropertyValue(name, out var value) ? value : null;

    private static string? ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static bool? ReadBool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out bool flag) ? flag : null;

    private static int? ReadInt(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out int number) ? number : null;

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add((JsonNode?)JsonValue.Create(value));
        }

        return array;
    }
}
