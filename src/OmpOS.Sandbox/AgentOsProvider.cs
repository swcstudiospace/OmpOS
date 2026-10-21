using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;

namespace OmpOS.Sandbox;

/// <summary>
/// agentOS sandbox provider over the hosted agentOS HTTP API (https://rivet.dev/agentos/docs/hosted-api/).
/// Verified calls: <c>PUT /actors?namespace=</c> (create or get a VM by key), <c>DELETE /actors/{id}?namespace=</c>
/// (destroy; 404 means already gone), and <c>POST /gateway/{id}/action/v1.{group}.{action}</c> for
/// <c>v1.vm.status</c>, <c>v1.config.patch</c> (guest environment) and <c>v1.process.run</c> (argv with captured stdio).
/// A request field without a verified mapping fails with <see cref="NotAvailableException"/> before any call is sent.
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
    private const string RunAction = "v1.process.run";
    private const string SnapshotNotSupported = "agentOS hosted API has no snapshot action (Q-SB-AGENTOS)";
    private const string LogsNotMapped = "agentOS output.read byte encoding over HTTP is not verified (Q-SB-AGENTOS)";
    private static readonly TimeSpan ControlTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _namespace;
    private readonly string _secretToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentOsProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client used for hosted API calls.</param>
    /// <param name="endpoint">Hosted API base URL, for example <c>https://api.rivet.dev</c>.</param>
    /// <param name="namespaceName">Rivet namespace that owns the VMs.</param>
    /// <param name="secretToken">Namespace secret token (<c>sk_</c>), sent as bearer for control-plane calls and as <c>x-rivet-token</c> for actions.</param>
    public AgentOsProvider(HttpClient httpClient, Uri endpoint, string namespaceName, string secretToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretToken);
        if (!endpoint.IsAbsoluteUri)
        {
            throw new ArgumentException("Endpoint must be an absolute URI.", nameof(endpoint));
        }

        _http = httpClient;
        _endpoint = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
        _namespace = namespaceName;
        _secretToken = secretToken;
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

    /// <inheritdoc />
    public async Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
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
        try
        {
            return await _http.SendAsync(request, deadline.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new NotAvailableException("agentOS hosted API is unreachable", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("agentOS request exceeded its deadline", ex);
        }
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
                ["ompos.session"] = request.SessionId,
                ["ompos.idempotency"] = request.IdempotencyKey,
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
            throw Unmapped("isolation other than process");
        }

        if (request.NetworkPolicy.OutboundDefault != OutboundDefault.Deny || request.NetworkPolicy.Rules.Count > 0)
        {
            throw Unmapped("outbound network policy");
        }

        if (request.Mounts.Count > 0)
        {
            throw Unmapped("mounts");
        }

        if (request.SecretRefs.Count > 0)
        {
            throw Unmapped("secret references");
        }

        if (request.Spec.Image.Length > 0)
        {
            throw Unmapped("image selection (hosted VMs are prebuilt actors)");
        }

        if (request.Spec.Command.Count > 0)
        {
            throw Unmapped("provisioning command");
        }

        if (request.Spec.WorkingDirectory.Length > 0)
        {
            throw Unmapped("provisioning working directory");
        }

        if (request.Spec.Limits is not null)
        {
            throw Unmapped("resource limits");
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
            throw Unmapped("per-exec working directory");
        }

        if (request.Environment.Count > 0)
        {
            throw Unmapped("per-exec environment");
        }

        if (request.Tty)
        {
            throw Unmapped("TTY allocation");
        }
    }

    private static NotAvailableException Unmapped(string field) =>
        new($"agentOS hosted API has no verified mapping for {field} (Q-SB-AGENTOS)");

    private static NotAvailableException Rejected(string operation, HttpStatusCode status) =>
        new($"agentOS {operation} failed: HTTP {(int)status}");

    private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
