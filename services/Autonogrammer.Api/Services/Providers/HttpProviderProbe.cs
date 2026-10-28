using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Autonogrammer.Contracts.Providers;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>
/// The live test over HTTP. It guards against server-side request forgery at the connection: the host name is resolved here, every address
/// must be public (<see cref="PublicAddressPolicy.IsPublic"/>), and the socket connects to one of those addresses, so a name that resolves to
/// a private address, or changes its answer between a check and the connection, never reaches one. Redirects are not followed, no key and no
/// body are sent, the reply is never read, and the result holds a status word, not what the provider said.
/// </summary>
internal sealed class HttpProviderProbe : IProviderProbe, IDisposable
{
    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;

    /// <summary>Initializes a new instance of the <see cref="HttpProviderProbe"/> class.</summary>
    /// <param name="options">The provider options.</param>
    public HttpProviderProbe(IOptions<ProvidersOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _timeout = TimeSpan.FromSeconds(options.Value.TestTimeoutSeconds);
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            ConnectCallback = ConnectToPublicAddressAsync,
        };
        try
        {
            _client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
            handler = null;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    /// <inheritdoc />
    public async Task<ProviderProbeResult> ProbeAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (IPAddress.TryParse(url.DnsSafeHost, out var literal) && !PublicAddressPolicy.IsPublic(literal))
        {
            return Blocked();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            return Classify(response.StatusCode);
        }
        catch (HttpRequestException exception) when (exception.InnerException is BlockedAddressException)
        {
            return Blocked();
        }
        catch (HttpRequestException)
        {
            return new ProviderProbeResult(ProviderTestStatus.Unreachable, "Nothing answered at the address.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderProbeResult(ProviderTestStatus.Unreachable, "The address did not answer in time.");
        }
    }

    private static ProviderProbeResult Classify(HttpStatusCode status) => (int)status switch
    {
        401 or 403 => new ProviderProbeResult(ProviderTestStatus.Unauthorized, "The address answered and asks for a key."),
        >= 500 => new ProviderProbeResult(ProviderTestStatus.Unreachable, "The address answered with a server error."),
        _ => new ProviderProbeResult(ProviderTestStatus.Reachable),
    };

    private static ProviderProbeResult Blocked() =>
        new(ProviderTestStatus.Blocked, "The service does not call addresses on its own machine or network.");

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0 || addresses.Any(static address => !PublicAddressPolicy.IsPublic(address)))
        {
            throw new BlockedAddressException();
        }

        Socket? socket = null;
        try
        {
            socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            var stream = new NetworkStream(socket, ownsSocket: true);
            socket = null;
            return stream;
        }
        finally
        {
            socket?.Dispose();
        }
    }
}
