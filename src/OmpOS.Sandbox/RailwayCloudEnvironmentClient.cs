using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OmpOS.Sandbox;

/// <summary>
/// <see cref="ICloudEnvironmentClient"/> over the Railway public GraphQL API (tier T2: one service and one volume per session).
/// </summary>
/// <remarks>
/// <para>
/// Every GraphQL operation is listed in <see cref="RailwayOperations"/> and tagged VERIFIED (shown by a documented example) or
/// UNVERIFIED: introspection needed. The container id is the Railway service id; the label (service name and volume name) is
/// <see cref="RailwayCloudEnvironmentOptions.ServiceNamePrefix"/> plus the sanitised session id.
/// </para>
/// <para>
/// Sleep: Railway sleeps a serverless service implicitly after about 5 to 10 minutes without outbound traffic; there is no
/// stop API. <see cref="RequestIdleAsync"/> therefore never claims the container sleeps: it reports
/// <see cref="CloudContainerPhase.Sleeping"/> only when the latest deployment is SLEEPING, otherwise
/// <see cref="CloudContainerPhase.Idle"/>. <see cref="EnsureAwakeAsync"/> probes the worker's health path, which wakes a slept
/// service (the first request may answer 502, which is retried for the wake window).
/// Serverless applies only when the container is created, so the create flow deploys once after the settings are applied.
/// </para>
/// <para>
/// Boot generation is an in-memory per-service counter: it starts at 1 and is incremented when a wake follows a reported
/// SLEEPING deployment. A restart of the control plane loses the counter (UNVERIFIED limitation: Railway exposes no wake counter
/// in its documentation).
/// </para>
/// <para>
/// Volume files are only reachable inside the worker; the Railway API has no file endpoint, so
/// <see cref="AppendVolumeFileAsync"/> and <see cref="ReadVolumeFileAsync"/> throw <see cref="CloudEnvironmentFailure.Unsupported"/>.
/// </para>
/// <para>
/// A failure after <c>serviceCreate</c> does not delete the half-built service. Within one client instance the next
/// <see cref="CreateContainerAsync"/> adopts it by label and re-applies variables and settings; after a restart use
/// <see cref="ReconcileContainerAsync"/>. A failure of <c>volumeUpdate</c> after <c>volumeCreate</c> leaves an unnamed volume
/// that <see cref="FindVolumeAsync"/> cannot match.
/// </para>
/// <para>
/// The API token and every variable value are redacted from exception messages and are never logged. The HTTP clients are owned
/// by the caller. Mutations are never retried.
/// </para>
/// </remarks>
public sealed class RailwayCloudEnvironmentClient : ICloudEnvironmentClient
{
    private static readonly Action<ILogger, string, string, Exception?> ServiceCreated =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(1, "RailwayServiceCreated"),
            "Created Railway service {ServiceName} ({ServiceId})");

    private readonly RailwayCloudEnvironmentOptions _options;
    private readonly RailwayGraphQlTransport _transport;
    private readonly WorkerReadinessProbe _probe;
    private readonly SecretRedactor _redactor = new();
    private readonly ILogger<RailwayCloudEnvironmentClient> _logger;
    private readonly ConcurrentDictionary<string, ServiceState> _states = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _incomplete = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="RailwayCloudEnvironmentClient"/> class.
    /// </summary>
    /// <param name="graphQlClient">The HTTP client for the GraphQL API; owned by the caller.</param>
    /// <param name="workerProbeClient">The HTTP client for worker health probes; owned by the caller.</param>
    /// <param name="options">The options; invalid values throw <see cref="ArgumentException"/>.</param>
    /// <param name="timeProvider">The clock for the wake window, the retry delay and an HTTP-date <c>Retry-After</c>.</param>
    /// <param name="logger">The logger; secrets are never logged.</param>
    public RailwayCloudEnvironmentClient(
        HttpClient graphQlClient,
        HttpClient workerProbeClient,
        RailwayCloudEnvironmentOptions options,
        TimeProvider timeProvider,
        ILogger<RailwayCloudEnvironmentClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(graphQlClient);
        ArgumentNullException.ThrowIfNull(workerProbeClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Validate(options);
        _options = options;
        _logger = logger ?? NullLogger<RailwayCloudEnvironmentClient>.Instance;
        _redactor.Add(options.ApiToken);
        foreach (var value in options.Variables.Values)
        {
            _redactor.Add(value);
        }

        _transport = new RailwayGraphQlTransport(graphQlClient, options.Endpoint, options.ApiToken, timeProvider, _redactor, _logger);
        _probe = new WorkerReadinessProbe(workerProbeClient, timeProvider, options.WakeTimeout, options.WakeRetryInterval, options.HealthPath);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Matches the service named after the session label, then derives the phase from its latest deployment:
    /// SLEEPING is <see cref="CloudContainerPhase.Sleeping"/>; no deployment yet, INITIALIZING, BUILDING, DEPLOYING, QUEUED,
    /// WAITING, NEEDS_APPROVAL and SUCCESS are <see cref="CloudContainerPhase.Running"/>. REMOVED and REMOVING throw
    /// <see cref="CloudEnvironmentFailure.NotFound"/>; FAILED, CRASHED, SKIPPED and unknown statuses throw
    /// <see cref="CloudEnvironmentFailure.InvalidResponse"/> so a broken service is never adopted.
    /// </remarks>
    public async Task<CloudContainer?> FindContainerAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var label = LabelOf(sessionId);
        var service = (await ListServicesAsync(cancellationToken).ConfigureAwait(false))
            .Find(node => string.Equals(node.Name, label, StringComparison.Ordinal));
        if (service?.Id is not { Length: > 0 } serviceId)
        {
            return null;
        }

        var phase = MapPhase(await LatestStatusAsync(serviceId, cancellationToken).ConfigureAwait(false), label);
        var state = _states.GetOrAdd(serviceId, _ => new ServiceState(label));
        return state.Report(serviceId, sessionId, phase, WorkerBaseUri(label));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Idempotent by label. A new service is created from the image, gets its variables (with deploys skipped), its instance
    /// settings (serverless sleep, health check, restart policy, one replica) and is then deployed once so the settings take
    /// effect: serverless applies only when the container is created, and a service created from an image deploys on creation.
    /// </remarks>
    public async Task<CloudContainer> CreateContainerAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var label = LabelOf(sessionId);
        var existing = await FindContainerAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return _incomplete.ContainsKey(existing.ContainerId)
                ? await ConfigureAsync(existing.ContainerId, sessionId, label, cancellationToken).ConfigureAwait(false)
                : existing;
        }

        var created = await _transport.SendAsync(
            "serviceCreate",
            RailwayOperations.ServiceCreate,
            new RailwayWire.ServiceCreateVariables(
                new RailwayWire.ServiceCreateInput(_options.ProjectId, label, new RailwayWire.ServiceSource(_options.Image))),
            RailwayJsonContext.Default.ServiceCreateRequest,
            RailwayJsonContext.Default.ServiceCreateResponse,
            cancellationToken).ConfigureAwait(false);
        var serviceId = created.ServiceCreate?.Id;
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            throw new CloudEnvironmentException("Railway serviceCreate returned no service id.", CloudEnvironmentFailure.InvalidResponse)
            {
                Operation = "serviceCreate",
            };
        }

        ServiceCreated(_logger, label, serviceId, null);
        _incomplete[serviceId] = true;
        _states.GetOrAdd(serviceId, _ => new ServiceState(label));
        return await ConfigureAsync(serviceId, sessionId, label, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Re-applies variables and settings to the session's existing service and deploys it once. Use it to finish a service that a
    /// failed <see cref="CreateContainerAsync"/> left half-built, for example after a control plane restart.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The running container.</returns>
    /// <exception cref="CloudEnvironmentException"><see cref="CloudEnvironmentFailure.NotFound"/> when the session has no service.</exception>
    public async Task<CloudContainer> ReconcileContainerAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var label = LabelOf(sessionId);
        var existing = await FindContainerAsync(sessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new CloudEnvironmentException(
                string.Create(CultureInfo.InvariantCulture, $"Session '{sessionId}' has no Railway service."),
                CloudEnvironmentFailure.NotFound);
        return await ConfigureAsync(existing.ContainerId, sessionId, label, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>Matches the project volume whose name equals the session label (CreateVolumeAsync names it so).</remarks>
    public async Task<CloudVolume?> FindVolumeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var label = LabelOf(sessionId);
        var volume = (await ListVolumesAsync(cancellationToken).ConfigureAwait(false))
            .Find(node => string.Equals(node.Name, label, StringComparison.Ordinal));
        return volume?.Id is { Length: > 0 } id ? new CloudVolume(id, sessionId) : null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Requires the session's service. Railway creates the volume attached to the service, mounted at
    /// <see cref="RailwayCloudEnvironmentOptions.VolumeMountPath"/>; it is then renamed to the session label so it can be found.
    /// Idempotent by label.
    /// </remarks>
    /// <exception cref="CloudEnvironmentException"><see cref="CloudEnvironmentFailure.NotFound"/> when the session has no service.</exception>
    public async Task<CloudVolume> CreateVolumeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var label = LabelOf(sessionId);
        var existing = await FindVolumeAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var service = (await ListServicesAsync(cancellationToken).ConfigureAwait(false))
            .Find(node => string.Equals(node.Name, label, StringComparison.Ordinal));
        if (service?.Id is not { Length: > 0 } serviceId)
        {
            throw new CloudEnvironmentException(
                string.Create(CultureInfo.InvariantCulture, $"Session '{sessionId}' has no Railway service, so a volume cannot be created."),
                CloudEnvironmentFailure.NotFound);
        }

        var created = await _transport.SendAsync(
            "volumeCreate",
            RailwayOperations.VolumeCreate,
            new RailwayWire.VolumeCreateVariables(
                new RailwayWire.VolumeCreateInput(_options.ProjectId, serviceId, _options.VolumeMountPath, _options.EnvironmentId)),
            RailwayJsonContext.Default.VolumeCreateRequest,
            RailwayJsonContext.Default.VolumeCreateResponse,
            cancellationToken).ConfigureAwait(false);
        var volumeId = created.VolumeCreate?.Id;
        if (string.IsNullOrWhiteSpace(volumeId))
        {
            throw new CloudEnvironmentException("Railway volumeCreate returned no volume id.", CloudEnvironmentFailure.InvalidResponse)
            {
                Operation = "volumeCreate",
            };
        }

        await _transport.SendAsync(
            "volumeUpdate",
            RailwayOperations.VolumeUpdate,
            new RailwayWire.VolumeUpdateVariables(volumeId, new RailwayWire.VolumeUpdateInput(label)),
            RailwayJsonContext.Default.VolumeUpdateRequest,
            RailwayJsonContext.Default.VolumeUpdateResponse,
            cancellationToken).ConfigureAwait(false);
        _states.GetOrAdd(serviceId, _ => new ServiceState(label)).RecordMountPath(_options.VolumeMountPath);
        return new CloudVolume(volumeId, sessionId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The volume is created attached, so nothing is sent unless <paramref name="mountPath"/> differs from the mount path the
    /// client recorded when it created the volume; then <c>volumeInstanceUpdate</c> changes it (the service needs a redeploy to
    /// apply it: see <see cref="RedeployAsync"/>).
    /// </remarks>
    public async Task AttachVolumeAsync(string containerId, string volumeId, string mountPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountPath);
        var state = await ResolveAsync(containerId, cancellationToken).ConfigureAwait(false);
        if (string.Equals(state.MountPath ?? _options.VolumeMountPath, mountPath, StringComparison.Ordinal))
        {
            return;
        }

        var result = await _transport.SendAsync(
            "volumeInstanceUpdate",
            RailwayOperations.VolumeInstanceUpdate,
            new RailwayWire.VolumeInstanceUpdateVariables(volumeId, new RailwayWire.VolumeInstanceUpdateInput(mountPath)),
            RailwayJsonContext.Default.VolumeInstanceUpdateRequest,
            RailwayJsonContext.Default.ScalarMutationResponse,
            cancellationToken).ConfigureAwait(false);
        RequireAccepted(result.VolumeInstanceUpdate, "volumeInstanceUpdate");
        state.RecordMountPath(mountPath);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Railway has no stop operation, so this only re-reads the latest deployment: <see cref="CloudContainerPhase.Sleeping"/> when it
    /// is SLEEPING, otherwise <see cref="CloudContainerPhase.Idle"/>. It never claims the container sleeps.
    /// </remarks>
    public async Task<CloudContainer> RequestIdleAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var state = await ResolveAsync(containerId, cancellationToken).ConfigureAwait(false);
        var status = await LatestStatusAsync(containerId, cancellationToken).ConfigureAwait(false);
        var phase = MapPhase(status, state.Name) == CloudContainerPhase.Sleeping ? CloudContainerPhase.Sleeping : CloudContainerPhase.Idle;
        return state.Report(containerId, null, phase, WorkerBaseUri(state.Name));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reads the latest deployment status, probes <c>GET {WorkerBaseUri}{HealthPath}</c> until it answers 2xx (502, 503, 504,
    /// connection failures and per-request timeouts are retried every
    /// <see cref="RailwayCloudEnvironmentOptions.WakeRetryInterval"/> for <see cref="RailwayCloudEnvironmentOptions.WakeTimeout"/>),
    /// and increments the boot generation when the container had been reported SLEEPING.
    /// </remarks>
    /// <exception cref="CloudEnvironmentException">
    /// <see cref="CloudEnvironmentFailure.WakeTimeout"/> when the worker does not answer in time, or
    /// <see cref="CloudEnvironmentFailure.Transport"/> when it answers a status that is not retried.
    /// </exception>
    public async Task<CloudContainer> EnsureAwakeAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var state = await ResolveAsync(containerId, cancellationToken).ConfigureAwait(false);
        var status = await LatestStatusAsync(containerId, cancellationToken).ConfigureAwait(false);
        var slept = state.LastPhase == CloudContainerPhase.Sleeping || status == RailwayDeploymentStatus.Sleeping;
        await _probe.WaitUntilReadyAsync(WorkerBaseUri(state.Name), cancellationToken).ConfigureAwait(false);
        return state.Woke(containerId, slept, WorkerBaseUri(state.Name));
    }

    /// <inheritdoc />
    /// <exception cref="CloudEnvironmentException">Always <see cref="CloudEnvironmentFailure.Unsupported"/>.</exception>
    public Task AppendVolumeFileAsync(string volumeId, string path, string text, CancellationToken cancellationToken = default) =>
        throw UnsupportedFiles();

    /// <inheritdoc />
    /// <exception cref="CloudEnvironmentException">Always <see cref="CloudEnvironmentFailure.Unsupported"/>.</exception>
    public Task<string?> ReadVolumeFileAsync(string volumeId, string path, CancellationToken cancellationToken = default) =>
        throw UnsupportedFiles();

    /// <summary>
    /// Sets service variables in the configured environment. Values are secrets: they are redacted from every message.
    /// </summary>
    /// <param name="containerId">The container (service) identifier.</param>
    /// <param name="variables">The variables to upsert; existing variables not listed are kept.</param>
    /// <param name="skipDeploys"><see langword="true"/> to avoid the redeploy Railway triggers after a variable change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when Railway accepted the variables.</returns>
    public async Task SetVariablesAsync(string containerId, IReadOnlyDictionary<string, string> variables, bool skipDeploys, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        ArgumentNullException.ThrowIfNull(variables);
        foreach (var value in variables.Values)
        {
            _redactor.Add(value);
        }

        var result = await _transport.SendAsync(
            "variableCollectionUpsert",
            RailwayOperations.VariableCollectionUpsert,
            new RailwayWire.VariableCollectionUpsertVariables(
                new RailwayWire.VariableCollectionUpsertInput(_options.ProjectId, _options.EnvironmentId, containerId, variables, false, skipDeploys)),
            RailwayJsonContext.Default.VariableCollectionUpsertRequest,
            RailwayJsonContext.Default.ScalarMutationResponse,
            cancellationToken).ConfigureAwait(false);
        RequireAccepted(result.VariableCollectionUpsert, "variableCollectionUpsert");
    }

    /// <summary>
    /// Redeploys the service's latest deployment so changed variables or a changed mount path take effect.
    /// </summary>
    /// <param name="containerId">The container (service) identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when Railway accepted the redeploy.</returns>
    public async Task RedeployAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        var result = await _transport.SendAsync(
            "serviceInstanceRedeploy",
            RailwayOperations.ServiceInstanceRedeploy,
            new RailwayWire.ServiceInstanceVariables(containerId, _options.EnvironmentId),
            RailwayJsonContext.Default.ServiceInstanceRequest,
            RailwayJsonContext.Default.ScalarMutationResponse,
            cancellationToken).ConfigureAwait(false);
        RequireAccepted(result.ServiceInstanceRedeploy, "serviceInstanceRedeploy");
    }

    /// <summary>
    /// Deletes the service and all its deployments.
    /// </summary>
    /// <param name="containerId">The container (service) identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when Railway confirmed the deletion.</returns>
    public async Task DeleteServiceAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        var result = await _transport.SendAsync(
            "serviceDelete",
            RailwayOperations.ServiceDelete,
            new RailwayWire.IdVariables(containerId),
            RailwayJsonContext.Default.IdRequest,
            RailwayJsonContext.Default.ScalarMutationResponse,
            cancellationToken).ConfigureAwait(false);
        RequireAccepted(result.ServiceDelete, "serviceDelete");
        _states.TryRemove(containerId, out _);
        _incomplete.TryRemove(containerId, out _);
    }

    /// <summary>
    /// Deletes a volume. This permanently destroys the volume and all its data, including the session transcript.
    /// </summary>
    /// <param name="volumeId">The volume identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when Railway confirmed the deletion.</returns>
    public async Task DeleteVolumeAsync(string volumeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeId);
        var result = await _transport.SendAsync(
            "volumeDelete",
            RailwayOperations.VolumeDelete,
            new RailwayWire.VolumeIdVariables(volumeId),
            RailwayJsonContext.Default.VolumeIdRequest,
            RailwayJsonContext.Default.ScalarMutationResponse,
            cancellationToken).ConfigureAwait(false);
        RequireAccepted(result.VolumeDelete, "volumeDelete");
    }

    /// <summary>
    /// Reads the status of the service's latest deployment.
    /// </summary>
    /// <param name="containerId">The container (service) identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The status; <see cref="RailwayDeploymentStatus.Unknown"/> when there is no deployment or the status is not recognised.</returns>
    public async Task<RailwayDeploymentStatus> GetDeploymentStatusAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        return await LatestStatusAsync(containerId, cancellationToken).ConfigureAwait(false) ?? RailwayDeploymentStatus.Unknown;
    }

    /// <summary>
    /// Maps a latest-deployment status to a container phase, or throws for a state that must not be adopted.
    /// </summary>
    /// <param name="status">The status, or null when the service has no deployment yet.</param>
    /// <param name="serviceName">The service name for the message.</param>
    /// <returns>The phase.</returns>
    internal static CloudContainerPhase MapPhase(RailwayDeploymentStatus? status, string serviceName)
    {
        switch (status)
        {
            case null:
            case RailwayDeploymentStatus.Initializing:
            case RailwayDeploymentStatus.Building:
            case RailwayDeploymentStatus.Deploying:
            case RailwayDeploymentStatus.Queued:
            case RailwayDeploymentStatus.Waiting:
            case RailwayDeploymentStatus.NeedsApproval:
            case RailwayDeploymentStatus.Success:
                return CloudContainerPhase.Running;
            case RailwayDeploymentStatus.Sleeping:
                return CloudContainerPhase.Sleeping;
            case RailwayDeploymentStatus.Removed:
            case RailwayDeploymentStatus.Removing:
                throw new CloudEnvironmentException(
                    string.Create(CultureInfo.InvariantCulture, $"The Railway service '{serviceName}' was removed (latest deployment {status})."),
                    CloudEnvironmentFailure.NotFound);
            default:
                throw new CloudEnvironmentException(
                    string.Create(CultureInfo.InvariantCulture, $"The Railway service '{serviceName}' has a latest deployment in status {status}; it is not adopted."),
                    CloudEnvironmentFailure.InvalidResponse);
        }
    }

    /// <summary>
    /// Parses a Railway deployment status string.
    /// </summary>
    /// <param name="status">The status text, for example <c>SLEEPING</c>.</param>
    /// <returns>The status; <see cref="RailwayDeploymentStatus.Unknown"/> for text that is not recognised.</returns>
    internal static RailwayDeploymentStatus ParseStatus(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "INITIALIZING" => RailwayDeploymentStatus.Initializing,
            "BUILDING" => RailwayDeploymentStatus.Building,
            "DEPLOYING" => RailwayDeploymentStatus.Deploying,
            "SUCCESS" => RailwayDeploymentStatus.Success,
            "FAILED" => RailwayDeploymentStatus.Failed,
            "CRASHED" => RailwayDeploymentStatus.Crashed,
            "REMOVED" => RailwayDeploymentStatus.Removed,
            "REMOVING" => RailwayDeploymentStatus.Removing,
            "SLEEPING" => RailwayDeploymentStatus.Sleeping,
            "SKIPPED" => RailwayDeploymentStatus.Skipped,
            "WAITING" => RailwayDeploymentStatus.Waiting,
            "QUEUED" => RailwayDeploymentStatus.Queued,
            "NEEDS_APPROVAL" => RailwayDeploymentStatus.NeedsApproval,
            _ => RailwayDeploymentStatus.Unknown,
        };

    private static void Validate(RailwayCloudEnvironmentOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiToken, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ProjectId, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.EnvironmentId, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Image, nameof(options));
        ArgumentNullException.ThrowIfNull(options.Endpoint, nameof(options));
        ArgumentNullException.ThrowIfNull(options.Variables, nameof(options));
        if (!options.Endpoint.IsAbsoluteUri || (options.Endpoint.Scheme != Uri.UriSchemeHttps && options.Endpoint.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("The endpoint must be an absolute http or https URI.", nameof(options));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(options.ServiceNamePrefix, nameof(options));
        if (options.ServiceNamePrefix.Length > RailwayServiceLabel.MaxPrefixLength
            || options.ServiceNamePrefix.Any(c => c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The service name prefix must be lowercase letters, digits and dashes, at most {RailwayServiceLabel.MaxPrefixLength} characters."),
                nameof(options));
        }

        RequireAbsolutePath(options.VolumeMountPath, nameof(options.VolumeMountPath));
        RequireAbsolutePath(options.HealthPath, nameof(options.HealthPath));
        if (!string.IsNullOrEmpty(options.HealthcheckPath))
        {
            RequireAbsolutePath(options.HealthcheckPath, nameof(options.HealthcheckPath));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(options.WorkerPort, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.WorkerPort, 65535, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.WorkerHostSuffix, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.WakeTimeout, TimeSpan.Zero, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.WakeRetryInterval, TimeSpan.Zero, nameof(options));
    }

    private static void RequireAbsolutePath(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] != '/')
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"{name} must be an absolute path starting with '/'."), name);
        }
    }

    private static void RequireAccepted(JsonElement? value, string operation)
    {
        if (value is null || value.Value.ValueKind == JsonValueKind.Null)
        {
            throw new CloudEnvironmentException(
                string.Create(CultureInfo.InvariantCulture, $"Railway {operation} returned no result."),
                CloudEnvironmentFailure.InvalidResponse)
            {
                Operation = operation,
            };
        }

        if (value.Value.ValueKind == JsonValueKind.False)
        {
            throw new CloudEnvironmentException(
                string.Create(CultureInfo.InvariantCulture, $"Railway {operation} reported that it did not apply."),
                CloudEnvironmentFailure.GraphQlError)
            {
                Operation = operation,
            };
        }
    }

    private static CloudEnvironmentException UnsupportedFiles() =>
        new("Volume files are only reachable inside the worker; the Railway API has no file endpoint", CloudEnvironmentFailure.Unsupported);

    private string LabelOf(string sessionId) => RailwayServiceLabel.Create(_options.ServiceNamePrefix, sessionId);

    private Uri WorkerBaseUri(string serviceName) =>
        new(string.Create(CultureInfo.InvariantCulture, $"http://{serviceName}{_options.WorkerHostSuffix}:{_options.WorkerPort}/"));

    private async Task<List<RailwayWire.IdNameNode>> ListServicesAsync(CancellationToken cancellationToken)
    {
        var data = await _transport.SendAsync(
            "projectServices",
            RailwayOperations.ProjectServices,
            new RailwayWire.IdVariables(_options.ProjectId),
            RailwayJsonContext.Default.IdRequest,
            RailwayJsonContext.Default.ProjectServicesResponse,
            cancellationToken).ConfigureAwait(false);
        return Nodes(data.Project?.Services);
    }

    private async Task<List<RailwayWire.IdNameNode>> ListVolumesAsync(CancellationToken cancellationToken)
    {
        var data = await _transport.SendAsync(
            "projectVolumes",
            RailwayOperations.ProjectVolumes,
            new RailwayWire.IdVariables(_options.ProjectId),
            RailwayJsonContext.Default.IdRequest,
            RailwayJsonContext.Default.ProjectVolumesResponse,
            cancellationToken).ConfigureAwait(false);
        return Nodes(data.Project?.Volumes);
    }

    private static List<RailwayWire.IdNameNode> Nodes(RailwayWire.Connection<RailwayWire.IdNameNode>? connection) =>
        connection?.Edges?.Where(edge => edge.Node is not null).Select(edge => edge.Node!).ToList() ?? [];

    private async Task<RailwayDeploymentStatus?> LatestStatusAsync(string serviceId, CancellationToken cancellationToken)
    {
        var data = await _transport.SendAsync(
            "deployments",
            RailwayOperations.Deployments,
            new RailwayWire.DeploymentsVariables(new RailwayWire.DeploymentListInput(_options.ProjectId, serviceId, _options.EnvironmentId), 1),
            RailwayJsonContext.Default.DeploymentsRequest,
            RailwayJsonContext.Default.DeploymentsResponse,
            cancellationToken).ConfigureAwait(false);
        var node = data.Deployments?.Edges?.FirstOrDefault(edge => edge.Node is not null)?.Node;
        return node is null ? null : ParseStatus(node.Status);
    }

    private async Task<ServiceState> ResolveAsync(string containerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        if (_states.TryGetValue(containerId, out var cached))
        {
            return cached;
        }

        var service = (await ListServicesAsync(cancellationToken).ConfigureAwait(false))
            .Find(node => string.Equals(node.Id, containerId, StringComparison.Ordinal));
        if (service?.Name is not { Length: > 0 } name)
        {
            throw new CloudEnvironmentException(
                string.Create(CultureInfo.InvariantCulture, $"Railway service '{containerId}' does not exist."),
                CloudEnvironmentFailure.NotFound);
        }

        return _states.GetOrAdd(containerId, _ => new ServiceState(name));
    }

    private async Task<CloudContainer> ConfigureAsync(string serviceId, string sessionId, string label, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, string>(_options.Variables, StringComparer.Ordinal);
        if (_options.SecretVariablesProvider is { } provider)
        {
            var secrets = await provider(label, cancellationToken).ConfigureAwait(false);
            foreach (var (name, value) in secrets)
            {
                _redactor.Add(value);
                variables[name] = value;
            }
        }

        await SetVariablesAsync(serviceId, variables, skipDeploys: true, cancellationToken).ConfigureAwait(false);
        var healthcheckPath = string.IsNullOrEmpty(_options.HealthcheckPath) ? _options.HealthPath : _options.HealthcheckPath;
        var update = await _transport.SendAsync(
            "serviceInstanceUpdate",
            RailwayOperations.ServiceInstanceUpdate,
            new RailwayWire.ServiceInstanceUpdateVariables(
                serviceId,
                _options.EnvironmentId,
                new RailwayWire.ServiceInstanceUpdateInput(_options.EnableServerless, healthcheckPath, "ON_FAILURE", 1)),
            RailwayJsonContext.Default.ServiceInstanceUpdateRequest,
            RailwayJsonContext.Default.ScalarMutationResponse,
            cancellationToken).ConfigureAwait(false);
        RequireAccepted(update.ServiceInstanceUpdate, "serviceInstanceUpdate");
        var deploy = await _transport.SendAsync(
            "serviceInstanceDeployV2",
            RailwayOperations.ServiceInstanceDeployV2,
            new RailwayWire.ServiceInstanceVariables(serviceId, _options.EnvironmentId),
            RailwayJsonContext.Default.ServiceInstanceRequest,
            RailwayJsonContext.Default.ScalarMutationResponse,
            cancellationToken).ConfigureAwait(false);
        RequireAccepted(deploy.ServiceInstanceDeployV2, "serviceInstanceDeployV2");
        _incomplete.TryRemove(serviceId, out _);
        var state = _states.GetOrAdd(serviceId, _ => new ServiceState(label));
        return state.Report(serviceId, sessionId, CloudContainerPhase.Running, WorkerBaseUri(label));
    }

    /// <summary>
    /// The in-memory facts the client keeps per service. Every member locks the instance.
    /// </summary>
    private sealed class ServiceState
    {
        private readonly object _gate = new();
        private int _generation = 1;
        private CloudContainerPhase _lastPhase = CloudContainerPhase.Running;
        private string? _mountPath;
        private string? _sessionId;

        public ServiceState(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public CloudContainerPhase LastPhase
        {
            get
            {
                lock (_gate)
                {
                    return _lastPhase;
                }
            }
        }

        public string? MountPath
        {
            get
            {
                lock (_gate)
                {
                    return _mountPath;
                }
            }
        }

        public void RecordMountPath(string mountPath)
        {
            lock (_gate)
            {
                _mountPath = mountPath;
            }
        }

        public CloudContainer Report(string serviceId, string? sessionId, CloudContainerPhase phase, Uri workerBaseUri)
        {
            lock (_gate)
            {
                _lastPhase = phase;
                _sessionId = sessionId ?? _sessionId;
                return new CloudContainer(serviceId, _sessionId ?? Name, phase, _generation) { WorkerBaseUri = workerBaseUri };
            }
        }

        public CloudContainer Woke(string serviceId, bool slept, Uri workerBaseUri)
        {
            lock (_gate)
            {
                if (slept)
                {
                    _generation++;
                }

                _lastPhase = CloudContainerPhase.Running;
                return new CloudContainer(serviceId, _sessionId ?? Name, CloudContainerPhase.Running, _generation) { WorkerBaseUri = workerBaseUri };
            }
        }
    }
}
