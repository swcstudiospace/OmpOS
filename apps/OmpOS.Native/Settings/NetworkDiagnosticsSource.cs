using System.Globalization;
using System.Security.Authentication;
using OmpOS.Harness.OmpConfig;
using OmpOS.Harness.Providers;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Settings;

/// <summary>
/// The Network page's diagnostics run. The endpoints are the base URLs of the providers OmpOS manages in omp's <c>models.yml</c> and the
/// base endpoint of every row of the provider catalogue that has one, one per host and port. Each endpoint gets ONE unauthenticated GET:
/// no header, no body, no credential, no query and no user information, a five second limit and at most three redirects. Any HTTP
/// status counts as reachable; a refused or unresolved connection is unreachable, a missed limit is timed out, and a failed secure
/// connection is a TLS failure. Nothing runs until <see cref="RunAsync"/> is called, and nothing is stored.
/// </summary>
internal sealed class NetworkDiagnosticsSource : INetworkDiagnosticsSource
{
    /// <summary>The most redirects one request follows.</summary>
    public const int MaxRedirects = 3;

    private const int MaxParallelRequests = 6;

    private readonly Func<CancellationToken, Task<IReadOnlyList<OmpManagedProvider>>> _readManaged;
    private readonly Func<HttpClient> _clientFactory;
    private readonly TimeProvider _time;
    private readonly TimeSpan _timeout;
    private readonly IReadOnlyList<ProviderEntry> _catalogue;

    /// <summary>Initializes a new instance of the <see cref="NetworkDiagnosticsSource"/> class.</summary>
    /// <param name="readManaged">Reads the providers OmpOS manages, or null for none.</param>
    /// <param name="clientFactory">Creates the client one run sends through, or null for <see cref="CreateDefaultClient"/>. The run disposes it.</param>
    /// <param name="timeProvider">The clock of the time limit and the latency, or null for the system clock.</param>
    /// <param name="timeout">How long one request may take, or null for <see cref="DefaultTimeout"/>.</param>
    /// <param name="catalogue">The catalogue rows whose endpoints are checked, or null for <see cref="ProviderCatalogue.All"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public NetworkDiagnosticsSource(
        Func<CancellationToken, Task<IReadOnlyList<OmpManagedProvider>>>? readManaged = null,
        Func<HttpClient>? clientFactory = null,
        TimeProvider? timeProvider = null,
        TimeSpan? timeout = null,
        IReadOnlyList<ProviderEntry>? catalogue = null)
    {
        if (timeout is { } limit && limit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The time limit must be positive.");
        }

        _readManaged = readManaged ?? (static _ => Task.FromResult<IReadOnlyList<OmpManagedProvider>>([]));
        _clientFactory = clientFactory ?? CreateDefaultClient;
        _time = timeProvider ?? TimeProvider.System;
        _timeout = timeout ?? DefaultTimeout;
        _catalogue = catalogue ?? ProviderCatalogue.All;
    }

    /// <summary>Gets how long one request may take.</summary>
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(5);

    /// <summary>Creates a source whose managed providers are the OmpOS blocks of omp's <c>models.yml</c>.</summary>
    /// <param name="modelsFile">The editor of <c>models.yml</c>; only its read is used.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="modelsFile"/> is null.</exception>
    public static NetworkDiagnosticsSource Over(OmpModelsFile modelsFile)
    {
        ArgumentNullException.ThrowIfNull(modelsFile);
        return new NetworkDiagnosticsSource(async cancellationToken => (await modelsFile.ReadManagedAsync(cancellationToken).ConfigureAwait(false)).Providers);
    }

    /// <summary>
    /// Creates the handler a run sends through: it follows at most <see cref="MaxRedirects"/> redirects, keeps no cookies, and carries no
    /// credentials of any kind.
    /// </summary>
    /// <returns>The handler. The caller owns it.</returns>
    public static SocketsHttpHandler CreateDefaultHandler() => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = MaxRedirects,
        UseCookies = false,
        PreAuthenticate = false,
        ConnectTimeout = DefaultTimeout,
    };

    /// <summary>Creates the client a run sends through, over <see cref="CreateDefaultHandler"/>.</summary>
    /// <returns>The client. The caller owns it.</returns>
    public static HttpClient CreateDefaultClient()
    {
        var handler = CreateDefaultHandler();
        try
        {
            var client = new HttpClient(handler, disposeHandler: true);
            handler = null;
            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task<NetworkDiagnosticsReport> RunAsync(CancellationToken cancellationToken)
    {
        var endpoints = await CollectAsync(cancellationToken).ConfigureAwait(false);
        using var client = _clientFactory();
        client.Timeout = Timeout.InfiniteTimeSpan;
        using var gate = new SemaphoreSlim(MaxParallelRequests);
        var checks = await Task.WhenAll(endpoints.Select(endpoint => CheckAsync(client, gate, endpoint, cancellationToken))).ConfigureAwait(false);
        return new NetworkDiagnosticsReport(checks, _time.GetUtcNow());
    }

    private static string HostOf(Uri uri) => uri.IsDefaultPort
        ? uri.Host
        : string.Create(CultureInfo.InvariantCulture, $"{uri.Host}:{uri.Port}");

    private static bool IsTlsFailure(HttpRequestException fault)
    {
        if (fault.HttpRequestError == HttpRequestError.SecureConnectionError)
        {
            return true;
        }

        for (Exception? inner = fault; inner is not null; inner = inner.InnerException)
        {
            if (inner is AuthenticationException)
            {
                return true;
            }
        }

        return false;
    }

    private static Uri WithoutUserInfoQueryAndFragment(Uri uri) => new UriBuilder(uri)
    {
        UserName = string.Empty,
        Password = string.Empty,
        Query = string.Empty,
        Fragment = string.Empty,
    }.Uri;

    private async Task<List<Endpoint>> CollectAsync(CancellationToken cancellationToken)
    {
        var endpoints = new List<Endpoint>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<OmpManagedProvider> managed;
        try
        {
            managed = await _readManaged(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException and not OutOfMemoryException)
        {
            // An unreadable models.yml leaves the catalogue endpoints to check; the Models page reports the unreadable file.
            managed = [];
        }

        foreach (var provider in managed)
        {
            Add(endpoints, seen, provider.Id, provider.BaseUrl);
        }

        foreach (var entry in _catalogue)
        {
            if (entry.BaseEndpoint is { } endpoint)
            {
                Add(endpoints, seen, entry.Id, endpoint);
            }
        }

        return endpoints;
    }

    private static void Add(List<Endpoint> endpoints, HashSet<string> seen, string name, Uri uri)
    {
        if (!uri.IsAbsoluteUri || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)))
        {
            return;
        }

        var key = string.Create(CultureInfo.InvariantCulture, $"{uri.Host}:{uri.Port}");
        if (seen.Add(key))
        {
            endpoints.Add(new Endpoint(name, HostOf(uri), WithoutUserInfoQueryAndFragment(uri)));
        }
    }

    private async Task<EndpointCheck> CheckAsync(HttpClient client, SemaphoreSlim gate, Endpoint endpoint, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await SendAsync(client, endpoint, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = gate.Release();
        }
    }

    private async Task<EndpointCheck> SendAsync(HttpClient client, Endpoint endpoint, CancellationToken cancellationToken)
    {
        var started = _time.GetTimestamp();
        using var limit = new CancellationTokenSource(_timeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint.Request);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            return Result(endpoint, EndpointStatus.Reachable, (int)response.StatusCode, started);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result(endpoint, EndpointStatus.TimedOut, null, started);
        }
        catch (HttpRequestException fault)
        {
            return Result(endpoint, IsTlsFailure(fault) ? EndpointStatus.TlsFailed : EndpointStatus.Unreachable, null, started);
        }
        catch (Exception fault) when (fault is not OperationCanceledException and not OutOfMemoryException)
        {
            // A request that could not even be formed (an unsupported scheme, for example) was not sent.
            return new EndpointCheck(endpoint.Name, endpoint.Host, EndpointStatus.NotChecked, null, null, NetworkCopy.CheckMessage(EndpointStatus.NotChecked, null));
        }
    }

    private EndpointCheck Result(Endpoint endpoint, EndpointStatus status, int? httpStatus, long started) =>
        new(endpoint.Name, endpoint.Host, status, httpStatus, _time.GetElapsedTime(started), NetworkCopy.CheckMessage(status, httpStatus));

    private sealed record Endpoint(string Name, string Host, Uri Request);
}
