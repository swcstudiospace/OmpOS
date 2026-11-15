using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;

namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>
/// Session-owned loopback HTTP and CONNECT proxy. It resolves names itself, classifies every answer, and dials the
/// pinned address. A plain-HTTP request keeps its method, end-to-end headers and bounded body; hop-by-hop headers and
/// Proxy-Authorization are never forwarded. Chromium credential wiring via Page.AuthenticateAsync is [INFERENCE] and is not asserted here.
/// </summary>
public sealed class EgressGuardProxy : IAsyncDisposable
{
    private const int Ok = 200;
    private const int MaxHeadBytes = 16384;
    private const int MaxRequestBodyBytes = 10 * 1024 * 1024;
    private const int MaxChunkLineBytes = 256;
    private const int MaxTrailerLines = 32;
    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();
    private static readonly byte[] LineBreak = "\r\n"u8.ToArray();
    private static readonly byte[] EmptyBody = [];
    private static readonly char[] BareLineBreaks = ['\r', '\n'];
    private static readonly HashSet<string> NotForwarded = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization", "Proxy-Connection", "TE", "Trailer",
        "Transfer-Encoding", "Upgrade", "Host", "Content-Length", "Expect",
    };

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
            var reader = PipeReader.Create(stream);
            try
            {
                var headBytes = await ReadUntilAsync(reader, HeadEnd, MaxHeadBytes, cancellationToken).ConfigureAwait(false);
                if (headBytes is null || !TryParse(Encoding.Latin1.GetString(headBytes), out var method, out var target, out var headers))
                {
                    await WriteStatusAsync(stream, 400, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (!CredentialsMatch(headers))
                {
                    await WriteStatusAsync(stream, 407, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
                {
                    await ForwardConnectAsync(reader, stream, target, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await ForwardPlainAsync(reader, stream, method, target, headers, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                await reader.CompleteAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task ForwardConnectAsync(PipeReader reader, NetworkStream stream, string target, CancellationToken cancellationToken)
    {
        if (!TryHostPort(target, out var host, out var port))
        {
            await WriteStatusAsync(stream, 403, cancellationToken).ConfigureAwait(false);
            return;
        }

        var (status, upstream) = await DialForwardAsync(host, port, cancellationToken).ConfigureAwait(false);
        if (upstream is null)
        {
            await WriteStatusAsync(stream, status, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using (upstream.ConfigureAwait(false))
        {
            await WriteStatusAsync(stream, Ok, cancellationToken).ConfigureAwait(false);
            await FlushBufferedAsync(reader, upstream, cancellationToken).ConfigureAwait(false);
            await RelayAsync(stream, upstream, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ForwardPlainAsync(
        PipeReader reader,
        NetworkStream stream,
        string method,
        string target,
        List<(string Name, string Value)> headers,
        CancellationToken cancellationToken)
    {
        if (!TryTarget(target, out var host, out var port, out var path))
        {
            await WriteStatusAsync(stream, 403, cancellationToken).ConfigureAwait(false);
            return;
        }

        var (bodyStatus, body) = await ReadRequestBodyAsync(reader, stream, headers, cancellationToken).ConfigureAwait(false);
        if (bodyStatus != Ok)
        {
            await WriteStatusAsync(stream, bodyStatus, cancellationToken).ConfigureAwait(false);
            return;
        }

        var (dialStatus, upstream) = await DialForwardAsync(host, port, cancellationToken).ConfigureAwait(false);
        if (upstream is null)
        {
            await WriteStatusAsync(stream, dialStatus, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using (upstream.ConfigureAwait(false))
        {
            var head = BuildUpstreamHead(method, path, host, port, headers, body.Length);
            await upstream.WriteAsync(Encoding.Latin1.GetBytes(head), cancellationToken).ConfigureAwait(false);
            await upstream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            await upstream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(int Status, Stream? Upstream)> DialForwardAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            var upstream = await DialAsync(host, port, cancellationToken).ConfigureAwait(false);
            if (upstream is null)
            {
                return (403, null);
            }

            return (Ok, upstream);
        }
        catch (EgressGuardUnavailableException)
        {
            return (503, null);
        }
    }

    private bool CredentialsMatch(List<(string Name, string Value)> headers)
    {
        var values = ValuesOf(headers, "Proxy-Authorization");
        if (values.Count != 1 || !values[0].StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(values[0]["Basic ".Length..].Trim()));
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

    private static bool TryTarget(string target, out string host, out int port, out string path)
    {
        host = string.Empty;
        port = 0;
        path = "/";
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

    private static bool TryParse(string text, out string method, out string target, out List<(string Name, string Value)> headers)
    {
        method = string.Empty;
        target = string.Empty;
        headers = [];
        var lines = text.Split("\r\n");
        var request = lines[0].Split(' ');
        if (request.Length < 2 || lines.Any(line => line.IndexOfAny(BareLineBreaks) >= 0))
        {
            return false;
        }

        method = request[0];
        target = request[1];
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (line.Length == 0 || line[0] is ' ' or '\t' || colon <= 0)
            {
                return false;
            }

            headers.Add((line[..colon].Trim(), line[(colon + 1)..].Trim()));
        }

        return true;
    }

    private static async Task<(int Status, byte[] Body)> ReadRequestBodyAsync(
        PipeReader reader,
        Stream stream,
        List<(string Name, string Value)> headers,
        CancellationToken cancellationToken)
    {
        var encodings = ValuesOf(headers, "Transfer-Encoding");
        var lengths = ValuesOf(headers, "Content-Length");
        if (encodings.Count == 0 && lengths.Count == 0)
        {
            return (Ok, EmptyBody);
        }

        if (lengths.Count > 1 || encodings.Count > 1 || (encodings.Count > 0 && lengths.Count > 0))
        {
            return (400, EmptyBody);
        }

        if (encodings.Count == 1)
        {
            if (!encodings[0].Equals("chunked", StringComparison.OrdinalIgnoreCase))
            {
                return (400, EmptyBody);
            }

            await SendContinueIfExpectedAsync(stream, headers, cancellationToken).ConfigureAwait(false);
            return await ReadChunkedBodyAsync(reader, cancellationToken).ConfigureAwait(false);
        }

        if (!long.TryParse(lengths[0], NumberStyles.None, CultureInfo.InvariantCulture, out var length))
        {
            return (400, EmptyBody);
        }

        if (length > MaxRequestBodyBytes)
        {
            return (413, EmptyBody);
        }

        await SendContinueIfExpectedAsync(stream, headers, cancellationToken).ConfigureAwait(false);
        var body = new byte[length];
        return await ReadExactAsync(reader, body, cancellationToken).ConfigureAwait(false) ? (Ok, body) : (400, EmptyBody);
    }

    private static async Task<(int Status, byte[] Body)> ReadChunkedBodyAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        using var body = new MemoryStream();
        while (true)
        {
            var sizeLine = await ReadUntilAsync(reader, LineBreak, MaxChunkLineBytes, cancellationToken).ConfigureAwait(false);
            if (sizeLine is null || !TryChunkSize(sizeLine, out var size))
            {
                return (400, EmptyBody);
            }

            if (size == 0)
            {
                var terminated = await DrainTrailersAsync(reader, cancellationToken).ConfigureAwait(false);
                return terminated ? (Ok, body.ToArray()) : (400, EmptyBody);
            }

            if (size > MaxRequestBodyBytes - body.Length)
            {
                return (413, EmptyBody);
            }

            var chunk = new byte[size];
            var terminator = new byte[2];
            if (!await ReadExactAsync(reader, chunk, cancellationToken).ConfigureAwait(false)
                || !await ReadExactAsync(reader, terminator, cancellationToken).ConfigureAwait(false)
                || !terminator.AsSpan().SequenceEqual(LineBreak.AsSpan()))
            {
                return (400, EmptyBody);
            }

            await body.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<bool> DrainTrailersAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        for (var i = 0; i < MaxTrailerLines; i++)
        {
            var line = await ReadUntilAsync(reader, LineBreak, MaxChunkLineBytes, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return false;
            }

            if (line.Length == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryChunkSize(byte[] line, out long size)
    {
        var text = Encoding.Latin1.GetString(line);
        var extension = text.IndexOf(';', StringComparison.Ordinal);
        if (extension >= 0)
        {
            text = text[..extension];
        }

        return long.TryParse(text.Trim(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out size);
    }

    private static async Task SendContinueIfExpectedAsync(Stream stream, List<(string Name, string Value)> headers, CancellationToken cancellationToken)
    {
        if (headers.Any(header => header.Name.Equals("Expect", StringComparison.OrdinalIgnoreCase)
            && header.Value.Contains("100-continue", StringComparison.OrdinalIgnoreCase)))
        {
            await stream.WriteAsync(Encoding.Latin1.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"), cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildUpstreamHead(string method, string path, string host, int port, List<(string Name, string Value)> headers, int bodyLength)
    {
        var builder = new StringBuilder();
        builder.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\n");
        builder.Append("Host: ").Append(Authority(host, port)).Append("\r\n");
        var connectionTokens = ConnectionTokens(headers);
        foreach (var header in headers)
        {
            if (IsForwardable(header.Name, connectionTokens))
            {
                builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
            }
        }

        var bodyless = method.Equals("GET", StringComparison.OrdinalIgnoreCase) || method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);
        if (bodyLength > 0 || !bodyless)
        {
            builder.Append("Content-Length: ").Append(bodyLength.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        }

        builder.Append("Connection: close\r\n\r\n");
        return builder.ToString();
    }

    private static string Authority(string host, int port)
    {
        var literal = host.AsSpan().Contains(':') && !host.StartsWith('[') ? "[" + host + "]" : host;
        return literal + ":" + port.ToString(CultureInfo.InvariantCulture);
    }

    private static HashSet<string> ConnectionTokens(List<(string Name, string Value)> headers)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in ValuesOf(headers, "Connection"))
        {
            foreach (var token in value.Split(','))
            {
                tokens.Add(token.Trim());
            }
        }

        return tokens;
    }

    private static bool IsForwardable(string name, HashSet<string> connectionTokens) =>
        !NotForwarded.Contains(name) && !connectionTokens.Contains(name);

    private static List<string> ValuesOf(List<(string Name, string Value)> headers, string name) =>
        headers.Where(header => header.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(header => header.Value).ToList();

    private static async Task FlushBufferedAsync(PipeReader reader, Stream upstream, CancellationToken cancellationToken)
    {
        if (!reader.TryRead(out var result))
        {
            return;
        }

        var position = result.Buffer.Start;
        while (result.Buffer.TryGet(ref position, out var memory))
        {
            await upstream.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
        }

        reader.AdvanceTo(result.Buffer.End);
    }

    private static async Task<byte[]?> ReadUntilAsync(PipeReader reader, byte[] delimiter, int maxBytes, CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;
            var sequence = new SequenceReader<byte>(buffer);
            if (sequence.TryReadTo(out ReadOnlySequence<byte> found, delimiter))
            {
                var bytes = found.ToArray();
                reader.AdvanceTo(sequence.Position);
                return bytes.Length <= maxBytes ? bytes : null;
            }

            reader.AdvanceTo(buffer.Start, buffer.End);
            if (result.IsCompleted || buffer.Length > maxBytes)
            {
                return null;
            }
        }
    }

    private static async Task<bool> ReadExactAsync(PipeReader reader, byte[] destination, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < destination.Length)
        {
            var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;
            var take = (int)Math.Min(buffer.Length, destination.Length - filled);
            buffer.Slice(0, take).CopyTo(destination.AsSpan(filled));
            reader.AdvanceTo(buffer.GetPosition(take));
            filled += take;
            if (take == 0 && result.IsCompleted)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task WriteStatusAsync(Stream stream, int status, CancellationToken cancellationToken)
    {
        var reason = status switch
        {
            200 => "Connection Established",
            400 => "Bad Request",
            407 => "Proxy Authentication Required",
            413 => "Payload Too Large",
            503 => EgressGuardUnavailableException.Token,
            _ => "Forbidden",
        };
        var body = status == Ok
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
