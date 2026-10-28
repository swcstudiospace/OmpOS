using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Native.Views;
using Autonogrammer.Presentation.Cloud;

namespace Autonogrammer.Native.Cloud;

/// <summary>
/// The desktop's <see cref="ICloudAgentsHost"/> over the Autonogrammer API's <c>/cloud/*</c> routes. Every call is one request with the user's bearer
/// token; a failed response becomes a <see cref="CloudHostException"/> carrying the problem's plain detail (never the token, the URL or the body),
/// a transport failure or a timeout becomes <see cref="CloudFailureKind.Unavailable"/>, and nothing else escapes. Without an API address the host is
/// not configured: <see cref="GetSetupAsync"/> says so and every other call throws <see cref="CloudFailureKind.NotConfigured"/>.
/// </summary>
/// <remarks>
/// <see cref="Changes"/> is raised after every call that changes an agent, and from a poll while at least one subscriber is attached: the poll
/// lists the agents every <see cref="DefaultPollInterval"/> and raises the event only when an agent appeared, went, changed status or finished
/// something. The API's per-agent SSE stream (<c>GET /cloud/agents/{id}/events</c>) carries a session log, not status changes, so it is not used here.
/// </remarks>
internal sealed class ApiCloudAgentsHost : ICloudAgentsHost, IDisposable
{
    /// <summary>Gets how often the poll lists the agents while the Cloud page is attached.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(15);

    private const string AgentsPath = "cloud/agents";
    private const string LimitsPath = "cloud/limits";
    private const string SecretsPath = "cloud/secrets";

    private static readonly Action<ILogger, string, Exception?> FirstPromptFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(40, "CloudFirstPromptFailed"),
            "Cloud agent {AgentId} was created but its first task could not be sent.");

    private readonly HttpClient _http;
    private readonly Uri? _baseAddress;
    private readonly TimeProvider _time;
    private readonly Func<CancellationToken, ValueTask<string?>>? _bearerToken;
    private readonly IUrlLauncher? _urls;
    private readonly ILogger _logger;
    private readonly TimeSpan _pollInterval;
    private readonly Lock _gate = new();

    private EventHandler? _changes;
    private int _subscribers;
    private ITimer? _timer;
    private int _polling;
    private string? _fingerprint;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="ApiCloudAgentsHost"/> class.</summary>
    /// <param name="httpClient">The client every call goes through. The host sets no base address and no default header on it and does not dispose it.</param>
    /// <param name="apiBaseAddress">The origin of the Autonogrammer API, for example <c>https://api.autonogrammer.example/</c>, or null when the head has no API configured. Must be an absolute http or https address.</param>
    /// <param name="timeProvider">The clock behind the poll.</param>
    /// <param name="bearerToken">Supplies the bearer token for each call, or null for unauthenticated calls. It returns null or blank when the user is not signed in; the call is then sent without a token and the API answers 401.</param>
    /// <param name="urlLauncher">Opens a pull request page in the system browser, or null when the head has no launcher.</param>
    /// <param name="logger">Receives warnings (a first task that could not be sent), or null to discard them.</param>
    /// <param name="pollInterval">How often the poll runs, or null for <see cref="DefaultPollInterval"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> or <paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="apiBaseAddress"/> is not an absolute http or https address.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pollInterval"/> is not positive.</exception>
    public ApiCloudAgentsHost(
        HttpClient httpClient,
        Uri? apiBaseAddress,
        TimeProvider timeProvider,
        Func<CancellationToken, ValueTask<string?>>? bearerToken = null,
        IUrlLauncher? urlLauncher = null,
        ILogger? logger = null,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (apiBaseAddress is not null && (!apiBaseAddress.IsAbsoluteUri
            || !(string.Equals(apiBaseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || string.Equals(apiBaseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))))
        {
            throw new ArgumentException("The API address must be an absolute http or https address.", nameof(apiBaseAddress));
        }

        if (pollInterval is { } interval && interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "The poll interval must be positive.");
        }

        _http = httpClient;
        _baseAddress = apiBaseAddress is null ? null : WithTrailingSlash(apiBaseAddress);
        _time = timeProvider;
        _bearerToken = bearerToken;
        _urls = urlLauncher;
        _logger = logger ?? NullLogger.Instance;
        _pollInterval = pollInterval ?? DefaultPollInterval;
    }

    /// <inheritdoc/>
    public event EventHandler? Changes
    {
        add
        {
            lock (_gate)
            {
                _changes += value;
                _subscribers++;
                if (_timer is null && !_disposed && _baseAddress is not null)
                {
                    _timer = _time.CreateTimer(static state => ((ApiCloudAgentsHost)state!).StartPoll(), this, _pollInterval, _pollInterval);
                }
            }
        }

        remove
        {
            lock (_gate)
            {
                _changes -= value;
                _subscribers = Math.Max(0, _subscribers - 1);
                if (_subscribers == 0)
                {
                    _timer?.Dispose();
                    _timer = null;
                    _fingerprint = null;
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<CloudSetup> GetSetupAsync(CancellationToken cancellationToken)
    {
        if (_baseAddress is null)
        {
            return CloudSetup.NotConfigured(CloudCopy.NotConfiguredIntro);
        }

        CloudLimitsResponse limits;
        try
        {
            limits = await SendAsync(HttpMethod.Get, LimitsPath, content: null, CloudJsonContext.Default.CloudLimitsResponse, cancellationToken).ConfigureAwait(false);
        }
        catch (CloudHostException ex) when (ex.Kind == CloudFailureKind.NotFound)
        {
            // The routes are not mapped when the API runs with cloud agents off: the installation is not set up for them.
            _ = ex;
            return CloudSetup.NotConfigured(CloudCopy.NotConfiguredIntro);
        }

        var secrets = await SendAsync(HttpMethod.Get, SecretsPath, content: null, CloudJsonContext.Default.CloudSecretListResponse, cancellationToken).ConfigureAwait(false);
        return new CloudSetup
        {
            IsConfigured = true,
            KnownSecretNames = [.. secrets.Secrets.Select(static secret => secret.Name).Order(StringComparer.Ordinal)],
            DefaultIdleMinutes = limits.DefaultIdleMinutes > 0 ? limits.DefaultIdleMinutes : 15,
        };
    }

    /// <inheritdoc/>
    public async Task<CloudLimits> GetLimitsAsync(CancellationToken cancellationToken)
    {
        RequireConfigured();
        var limits = await SendAsync(HttpMethod.Get, LimitsPath, content: null, CloudJsonContext.Default.CloudLimitsResponse, cancellationToken).ConfigureAwait(false);
        return new CloudLimits(
            limits.Agents,
            limits.MaxAgents > 0 ? limits.MaxAgents : null,
            limits.Running,
            limits.MaxConcurrentRunning > 0 ? limits.MaxConcurrentRunning : null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Presentation.Cloud.CloudAgentRecord>> ListAsync(CancellationToken cancellationToken)
    {
        RequireConfigured();
        var list = await SendAsync(HttpMethod.Get, AgentsPath, content: null, CloudJsonContext.Default.CloudAgentListResponse, cancellationToken).ConfigureAwait(false);
        return [.. list.Agents.Select(ApiCloudAgentMapper.ToRecord)];
    }

    /// <inheritdoc/>
    public async Task<Presentation.Cloud.CloudAgentRecord> CreateAsync(CloudAgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireConfigured();
        var body = new CreateCloudAgentRequest
        {
            Name = request.Name,
            Repositories =
            [
                .. request.Repositories.Select(repository => new Contracts.Cloud.CloudRepository
                {
                    Address = repository.Address,
                    Branch = request.Branch,
                    BaseBranch = request.BaseBranch,
                }),
            ],
            EnvironmentId = request.EnvironmentId,
            SecretRefs = request.SecretNames.Count == 0 ? null : [.. request.SecretNames],
            IdleMinutes = request.IdleMinutes > 0 ? request.IdleMinutes : null,
            Model = request.Model,
        };
        using var createContent = JsonContent.Create(body, CloudJsonContext.Default.CreateCloudAgentRequest);
        var created = await SendAsync(HttpMethod.Post, AgentsPath, createContent, CloudJsonContext.Default.CloudAgentResponse, cancellationToken).ConfigureAwait(false);
        var record = ApiCloudAgentMapper.ToRecord(created);
        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            try
            {
                await SendPromptAsync(created.Id, request.Prompt, cancellationToken).ConfigureAwait(false);
            }
            catch (CloudHostException ex)
            {
                // The agent exists and costs nothing yet: do not hide it behind the failure of its first task. The record says what happened.
                FirstPromptFailed(_logger, created.Id, ex);
                record = record with { StatusDetail = "The agent was created but its first task could not be sent." };
            }
        }

        RaiseChanges();
        return record;
    }

    /// <inheritdoc/>
    public Task<Presentation.Cloud.CloudAgentRecord> WakeAsync(string agentId, CancellationToken cancellationToken) =>
        TransitionAsync(agentId, "wake", cancellationToken);

    /// <inheritdoc/>
    public Task<Presentation.Cloud.CloudAgentRecord> StopAsync(string agentId, CancellationToken cancellationToken) =>
        TransitionAsync(agentId, "stop", cancellationToken);

    /// <inheritdoc/>
    public async Task DeleteAsync(string agentId, CancellationToken cancellationToken)
    {
        RequireConfigured();
        await SendAsync(HttpMethod.Delete, AgentPath(agentId), content: null, cancellationToken).ConfigureAwait(false);
        RaiseChanges();
    }

    /// <inheritdoc/>
    public async Task PromptAsync(string agentId, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        RequireConfigured();
        await SendPromptAsync(agentId, text, cancellationToken).ConfigureAwait(false);
        RaiseChanges();
    }

    /// <inheritdoc/>
    public async Task<CloudPullRequestLink> CreatePullRequestAsync(string agentId, CloudPullRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireConfigured();
        var body = new CreateCloudPullRequestRequest
        {
            Repository = request.Repository,
            Title = request.Title,
            Body = request.Body,
            BaseBranch = request.BaseBranch,
            Draft = request.Draft,
        };
        using var content = JsonContent.Create(body, CloudJsonContext.Default.CreateCloudPullRequestRequest);
        var opened = await SendAsync(
            HttpMethod.Post,
            AgentPath(agentId) + "/pull-request",
            content,
            CloudJsonContext.Default.CloudPullRequest,
            cancellationToken).ConfigureAwait(false);
        RaiseChanges();
        return ApiCloudAgentMapper.ToLink(opened, request.Title)
            ?? throw new CloudHostException(CloudFailureKind.Rejected, "The service returned a pull request address that cannot be opened.");
    }

    /// <inheritdoc/>
    public async Task OpenPullRequestAsync(CloudPullRequestLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (!string.Equals(link.Url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new CloudHostException(CloudFailureKind.Rejected, "Only https pull request pages can be opened.");
        }

        if (_urls is null || !await _urls.OpenAsync(link.Url, cancellationToken).ConfigureAwait(false))
        {
            throw new CloudHostException(CloudFailureKind.Unavailable, "No browser could be started. Open the pull request from your git host.");
        }
    }

    /// <summary>Stops the poll. The HTTP client is the caller's and is not disposed.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private static Uri WithTrailingSlash(Uri address)
    {
        var text = address.AbsoluteUri;
        return text.EndsWith('/') ? address : new Uri(text + "/", UriKind.Absolute);
    }

    private static string AgentPath(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        return AgentsPath + "/" + Uri.EscapeDataString(agentId);
    }

    private static string Fingerprint(IReadOnlyList<Presentation.Cloud.CloudAgentRecord> agents) =>
        string.Join(
            '|',
            agents
                .OrderBy(static agent => agent.Id, StringComparer.Ordinal)
                .Select(static agent => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{agent.Id}:{(int)agent.Status}:{agent.LastActivityAt?.UtcTicks}:{agent.PullRequests.Count}")));

    private void RequireConfigured()
    {
        if (_baseAddress is null)
        {
            throw new CloudHostException(CloudFailureKind.NotConfigured, ApiCloudFailures.FallbackMessage(CloudFailureKind.NotConfigured));
        }
    }

    private async Task<Presentation.Cloud.CloudAgentRecord> TransitionAsync(string agentId, string action, CancellationToken cancellationToken)
    {
        RequireConfigured();
        var agent = await SendAsync(HttpMethod.Post, AgentPath(agentId) + "/" + action, content: null, CloudJsonContext.Default.CloudAgentResponse, cancellationToken).ConfigureAwait(false);
        RaiseChanges();
        return ApiCloudAgentMapper.ToRecord(agent);
    }

    private async Task SendPromptAsync(string agentId, string text, CancellationToken cancellationToken)
    {
        var body = new CloudPromptRequest { Prompt = text };
        using var content = JsonContent.Create(body, CloudJsonContext.Default.CloudPromptRequest);
        await SendAsync(HttpMethod.Post, AgentPath(agentId) + "/prompts", content, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        using var response = await ExchangeAsync(method, path, content, cancellationToken).ConfigureAwait(false);
        try
        {
            var value = await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken).ConfigureAwait(false);
            return value ?? throw new CloudHostException(CloudFailureKind.Unavailable, "The Autonogrammer service sent an empty answer.");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            throw new CloudHostException(CloudFailureKind.Unavailable, "The Autonogrammer service sent an answer this version cannot read.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new CloudHostException(CloudFailureKind.Unavailable, ApiCloudFailures.FallbackMessage(CloudFailureKind.Unavailable), ex);
        }
    }

    private async Task SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var response = await ExchangeAsync(method, path, content, cancellationToken).ConfigureAwait(false);
        _ = response;
    }

    private async Task<HttpResponseMessage> ExchangeAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(_baseAddress!, path)) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (_bearerToken is not null && await _bearerToken(cancellationToken).ConfigureAwait(false) is { Length: > 0 } token && !string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new CloudHostException(CloudFailureKind.Unavailable, ApiCloudFailures.FallbackMessage(CloudFailureKind.Unavailable), ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        try
        {
            throw await ApiCloudFailures.FromResponseAsync(response, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            response.Dispose();
        }
    }

    private void RaiseChanges()
    {
        EventHandler? handlers;
        lock (_gate)
        {
            handlers = _changes;
        }

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A subscriber must never break a call or the other subscribers.
                _ = ex;
            }
        }
    }

    private void StartPoll()
    {
        if (Interlocked.Exchange(ref _polling, 1) != 0)
        {
            return;
        }

        _ = PollGuardedAsync();
    }

    private async Task PollGuardedAsync()
    {
        try
        {
            await PollOnceAsync().ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _polling, 0);
        }
    }

    /// <summary>
    /// Lists the agents once and raises <see cref="Changes"/> when the list differs from the previous poll. The first poll only records the
    /// baseline. A failed poll keeps the previous baseline.
    /// </summary>
    /// <returns>A task that completes when the poll is done. It never faults with a service failure.</returns>
    internal async Task PollOnceAsync()
    {
        try
        {
            var agents = await ListAsync(CancellationToken.None).ConfigureAwait(false);
            var current = Fingerprint(agents);
            bool changed;
            lock (_gate)
            {
                changed = _fingerprint is not null && !string.Equals(_fingerprint, current, StringComparison.Ordinal);
                _fingerprint = current;
            }

            if (changed)
            {
                RaiseChanges();
            }
        }
        catch (CloudHostException ex)
        {
            // The next poll decides; the page shows its own failures when it lists.
            _ = ex;
        }
    }
}
