using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;

namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>
/// Session-owned loopback HTTP and CONNECT proxy. It resolves names itself, classifies every answer, and dials the
/// pinned address. Chromium credential wiring via Page.AuthenticateAsync is [INFERENCE] and is not asserted here.
/// </summary>
public sealed class EgressGuardProxy : IAsyncDisposable
{
    private readonly BrowserUseOptions _options;
    private readonly OriginGrants _grants;
    private readonly IHostResolver _resolver;
    private readonly ITcpConnector _connector;
    private readonly bool _dispatchTurn;
    private readonly CancellationTokenSource _stop = new();
    private TcpListener? _listener;
    private Task? _accept;
    private int _running;

    /// <summary>Initializes the guard. It does not listen until <see cref="StartAsync"/>.</summary>
    /// <param name="options">The policy options.</param>
    /// <param name="grants">The session local-origin grants.</param>
    /// <param name="resolver">The resolver used for both decision and dial.</param>
    /// <param name="connector">The pinned connector. The default dials with a socket.</param>
    /// <param name="dispatchTurn">Whether Open is ignored.</param>
    public EgressGuardProxy(
        BrowserUseOptions options,
        OriginGrants grants,
        IHostResolver resolver,
        ITcpConnector? connector = null,
        bool dispatchTurn = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(resolver);
        _options = options;
        _grants = grants;
        _resolver = resolver;
        _connector = connector ?? new SocketTcpConnector();
        _dispatchTurn = dispatchTurn;
        UserName = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        Password = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    }

    /// <summary>Gets the per-session proxy user.</summary>
    public string UserName { get; }

    /// <summary>Gets the per-session proxy password.</summary>
    public string Password { get; }

    /// <summary>Gets the loopback port. Zero until started.</summary>
    public int Port { get; private set; }

    /// <summary>Gets a value indicating whether the guard is accepting.</summary>
    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>Starts the loopback listener.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        try
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Volatile.Write(ref _running, 1);
            _accept = AcceptLoopAsync(listener, _stop.Token);
            return Task.CompletedTask;
        }
        catch (Exception exception) when (exception is SocketException or InvalidOperationException)
        {
            throw new EgressGuardUnavailableException(EgressGuardUnavailableException.Token, exception);
        }
    }

    /// <summary>Dials through the guard. Throws <see cref="EgressGuardUnavailableException"/> when the guard is down.</summary>
    /// <param name="host">The host.</param>
    /// <param name="port">The port.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The pinned stream, or null when the target is refused.</returns>
    public ValueTask<Stream?> DialAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            throw new EgressGuardUnavailableException();
        }

        return PinnedDial.ConnectAsync(host, port, _resolver, _grants, _options, _connector, _dispatchTurn, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Volatile.Write(ref _running, 0);
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener?.Stop();
        (_listener as IDisposable)?.Dispose();
        _listener = null;
        Observe(_accept);
        _stop.Dispose();
    }

    private static void Observe(Task? task)
    {
        if (task is null)
        {
            return;
        }

        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                break;
            }

            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            if (!IsRunning)
            {
                return;
            }

            var stream = client.GetStream();
            var headerBytes = await ReadHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
            if (headerBytes is null || !TryParse(Encoding.ASCII.GetString(headerBytes), out var method, out var target, out var headers))
            {
                await WriteStatusAsync(stream, 400, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!CredentialsMatch(headers))
            {
                await WriteStatusAsync(stream, 407, cancellationToken).ConfigureAwait(false);
                return;
            }

            await ForwardAsync(stream, method, target, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ForwardAsync(NetworkStream stream, string method, string target, CancellationToken cancellationToken)
    {
        if (!TryTarget(method, target, out var host, out var port, out var path))
        {
            await WriteStatusAsync(stream, 403, cancellationToken).ConfigureAwait(false);
            return;
        }

        Stream? upstream;
        try
        {
            upstream = await DialAsync(host, port, cancellationToken).ConfigureAwait(false);
        }
        catch (EgressGuardUnavailableException)
        {
            await WriteStatusAsync(stream, 503, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (upstream is null)
        {
            await WriteStatusAsync(stream, 403, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using (upstream.ConfigureAwait(false))
        {
            if (method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                await WriteStatusAsync(stream, 200, cancellationToken).ConfigureAwait(false);
                await RelayAsync(stream, upstream, cancellationToken).ConfigureAwait(false);
                return;
            }

            var rewritten = "GET " + path + " HTTP/1.1\r\nHost: " + host + ":" + port.ToString(CultureInfo.InvariantCulture) + "\r\nConnection: close\r\n\r\n";
            await upstream.WriteAsync(Encoding.ASCII.GetBytes(rewritten), cancellationToken).ConfigureAwait(false);
            await upstream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool CredentialsMatch(Dictionary<string, string> headers)
    {
        if (!headers.TryGetValue("proxy-authorization", out var value) || !value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value["Basic ".Length..].Trim()));
        }
        catch (FormatException)
        {
            return false;
        }

        var split = decoded.IndexOf(':', StringComparison.Ordinal);
        if (split <= 0)
        {
            return false;
        }

        var userBytes = Encoding.UTF8.GetBytes(decoded[..split]);
        var passwordBytes = Encoding.UTF8.GetBytes(decoded[(split + 1)..]);
        var expectedUser = Encoding.UTF8.GetBytes(UserName);
        var expectedPassword = Encoding.UTF8.GetBytes(Password);
        if (userBytes.Length != expectedUser.Length || passwordBytes.Length != expectedPassword.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(userBytes, expectedUser)
            && CryptographicOperations.FixedTimeEquals(passwordBytes, expectedPassword);
    }

    private static bool TryTarget(string method, string target, out string host, out int port, out string path)
    {
        host = string.Empty;
        port = 0;
        path = "/";
        if (method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
        {
            return TryHostPort(target, out host, out port);
        }

        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        host = uri.IdnHost;
        port = uri.Port;
        path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
        return true;
    }

    private static bool TryHostPort(string target, out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        var split = target.LastIndexOf(':');
        if (split <= 0 || !int.TryParse(target[(split + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out port))
        {
            return false;
        }

        host = target[..split].Trim();
        if (host.Length >= 2 && host[0] == '[' && host[^1] == ']')
        {
            host = host[1..^1];
        }

        return host.Length > 0;
    }

    private static bool TryParse(string text, out string method, out string target, out Dictionary<string, string> headers)
    {
        method = string.Empty;
        target = string.Empty;
        headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = text.Split("\r\n");
        if (lines.Length == 0)
        {
            return false;
        }

        var request = lines[0].Split(' ');
        if (request.Length < 2)
        {
            return false;
        }

        method = request[0];
        target = request[1];
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
            {
                break;
            }

            var colon = lines[i].IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            headers[lines[i][..colon].Trim()] = lines[i][(colon + 1)..].Trim();
        }

        return true;
    }

    private static async Task<byte[]?> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var window = new byte[1024];
        while (buffer.Length < 16384)
        {
            var read = await stream.ReadAsync(window, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            await buffer.WriteAsync(window.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            if (buffer.GetBuffer().AsSpan(0, (int)buffer.Length).IndexOf("\r\n\r\n"u8) >= 0)
            {
                return buffer.ToArray();
            }
        }

        return null;
    }

    private static async Task WriteStatusAsync(Stream stream, int status, CancellationToken cancellationToken)
    {
        var reason = status switch
        {
            200 => "Connection Established",
            407 => "Proxy Authentication Required",
            503 => EgressGuardUnavailableException.Token,
            _ => "Forbidden",
        };
        var body = status == 200
            ? "HTTP/1.1 200 Connection Established\r\n\r\n"
            : "HTTP/1.1 " + status.ToString(CultureInfo.InvariantCulture) + " " + reason + "\r\nContent-Length: 0\r\nProxy-Authenticate: Basic realm=\"egress\"\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(body), cancellationToken).ConfigureAwait(false);
    }

    private static async Task RelayAsync(Stream left, Stream right, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var forward = CopyQuietAsync(left, right, linked.Token);
        var backward = CopyQuietAsync(right, left, linked.Token);
        await Task.WhenAny(forward, backward).ConfigureAwait(false);
        await linked.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(forward, backward).ConfigureAwait(false);
    }

    private static async Task CopyQuietAsync(Stream from, Stream to, CancellationToken cancellationToken)
    {
        try
        {
            await from.CopyToAsync(to, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
        {
            // The peer closed the tunnel.
        }
    }
}
