using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Harness.Secrets;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts.Desktop;

/// <summary>
/// A one-shot HTTP listener on the loopback interface that waits for the magic link callback <c>GET /cb/&lt;nonce&gt;?code=...</c>. It is written by hand over
/// <see cref="TcpListener"/> (not <c>HttpListener</c>: no Windows URL reservation), binds only <see cref="IPAddress.Loopback"/> on an ephemeral port, and
/// enforces these limits: a request line of at most 2 KiB, headers of at most 8 KiB, a 5 second read timeout, the method GET only, a <c>Host</c> header equal to
/// <c>127.0.0.1:&lt;port&gt;</c>, a nonce compared in constant time, a life of ten minutes, and exactly one valid callback.
/// </summary>
/// <remarks>
/// <para>
/// A request that breaks a limit is answered with a fixed page and does not end the wait: a stray local probe must not be able to cancel a sign-in. The
/// callback with the right nonce that reports an <c>error</c> or <c>error_code</c>, and the end of the ten minute life, end it with a failed
/// <see cref="LoopbackResult"/> (the sign-in error <c>LinkInvalidOrExpired</c>). The query text is never logged, shown or echoed; the page text is fixed.
/// </para>
/// <para>The listener never opens a browser. Dispose it to stop listening; disposing before a callback cancels <see cref="Result"/>.</para>
/// </remarks>
public sealed class LoopbackListener : IAsyncDisposable
{
    /// <summary>The longest request line the listener reads, in bytes, without the line end.</summary>
    public const int MaxRequestLineBytes = 2048;

    /// <summary>The largest block of headers the listener reads, in bytes, including the empty line that ends it.</summary>
    public const int MaxHeaderBytes = 8192;

    /// <summary>The path that precedes the nonce.</summary>
    public const string CallbackPrefix = "/cb/";

    private const int MaxConcurrentConnections = 8;
    private const int MaxCodeLength = 1024;
    private const int MaxDrainBytes = 64 * 1024;

    private const string ClosePage = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Autonogrammer</title></head><body><p>You can close this tab and go back to Autonogrammer.</p></body></html>";
    private const string InvalidPage = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Autonogrammer</title></head><body><p>This link did not work: close this tab and use the code from the email in Autonogrammer.</p></body></html>";

    private readonly TcpListener _listener;
    private readonly byte[] _nonce;
    private readonly string _hostHeader;
    private readonly TimeProvider _time;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _acceptStop = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource<LoopbackResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ITimer? _lifetimeTimer;
    private Task _acceptLoop = Task.CompletedTask;
    private int _finished;
    private int _connections;
    private int _rejected;
    private int _disposed;

    private LoopbackListener(TcpListener listener, string nonce, TimeProvider timeProvider, ILogger? logger)
    {
        _listener = listener;
        _nonce = Encoding.ASCII.GetBytes(nonce);
        _time = timeProvider;
        _logger = logger;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _hostHeader = IPAddress.Loopback + ":" + Port.ToString(CultureInfo.InvariantCulture);
        RedirectUri = new Uri("http://" + _hostHeader + CallbackPrefix + nonce, UriKind.Absolute);
    }

    /// <summary>Gets the life of the listener: after this long without a valid callback it ends as <see cref="LoopbackOutcome.Expired"/>.</summary>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets how long the listener waits for the bytes of one request.</summary>
    public static TimeSpan ReadTimeout { get; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets the ephemeral loopback port the listener is bound to.</summary>
    public int Port { get; }

    /// <summary>Gets the address to give the auth service as <c>redirect_to</c>: <c>http://127.0.0.1:&lt;port&gt;/cb/&lt;nonce&gt;</c>.</summary>
    public Uri RedirectUri { get; }

    /// <summary>Gets how many requests were rejected so far because they broke a limit. A rejection does not end the wait.</summary>
    public int RejectedRequests => Volatile.Read(ref _rejected);

    /// <summary>Gets the task that completes with the one valid callback, the error callback or the expiry, and is cancelled when the listener is disposed first.</summary>
    public Task<LoopbackResult> Result => _result.Task.WaitAsync(CancellationToken.None);

    /// <summary>Binds the loopback interface and starts listening.</summary>
    /// <param name="nonce">The nonce the callback path must carry, as the sign-in coordinator created it.</param>
    /// <param name="timeProvider">The clock of the read timeout and the ten minute life.</param>
    /// <param name="logger">An optional logger. It never receives a port, path or query.</param>
    /// <returns>The running listener, or null when no loopback port could be bound (the code is then the only way to sign in).</returns>
    public static LoopbackListener? TryStart(string nonce, TimeProvider timeProvider, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        ArgumentNullException.ThrowIfNull(timeProvider);

        TcpListener? tcp = new(IPAddress.Loopback, 0);
        try
        {
            tcp.Start(MaxConcurrentConnections);
            var listener = new LoopbackListener(tcp, nonce, timeProvider, logger);
            tcp = null;
            listener.Begin();
            return listener;
        }
        catch (SocketException)
        {
            DesktopLogMessages.LoopbackBindFailed(logger);
            return null;
        }
        finally
        {
            tcp?.Dispose();
        }
    }

    /// <summary>Stops listening, closes the socket and cancels <see cref="Result"/> when it has not completed.</summary>
    /// <returns>A task that completes once the accept loop has ended.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _ = _result.TrySetCanceled();
        _lifetimeTimer?.Dispose();
        await _acceptStop.CancelAsync().ConfigureAwait(false);
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        await _acceptLoop.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        _acceptStop.Dispose();
        _stop.Dispose();
        DesktopLogMessages.LoopbackClosed(_logger);
    }

    /// <summary>Names the type. Neither the port nor the nonce is included.</summary>
    /// <returns>The type name.</returns>
    public override string ToString() => nameof(LoopbackListener);

    private static string ReasonPhrase(int status) => status switch
    {
        200 => "OK",
        400 => "Bad Request",
        404 => "Not Found",
        405 => "Method Not Allowed",
        414 => "URI Too Long",
        431 => "Request Header Fields Too Large",
        _ => "Error",
    };

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string page, CancellationToken cancellationToken)
    {
        byte[] body = Encoding.UTF8.GetBytes(page);
        string head = string.Create(
            CultureInfo.InvariantCulture,
            $"HTTP/1.1 {status} {ReasonPhrase(status)}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'\r\nConnection: close\r\n");
        if (status == 405)
        {
            head += "Allow: GET\r\n";
        }

        head += "\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DrainAsync(TcpClient client, NetworkStream stream, CancellationToken cancellationToken)
    {
        // Ends our side first so the peer sees the whole response, then reads what the peer still sends so closing does not reset the connection.
        client.Client.Shutdown(SocketShutdown.Send);
        byte[] scratch = new byte[1024];
        int total = 0;
        while (total < MaxDrainBytes)
        {
            int read = await stream.ReadAsync(scratch, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            total += read;
        }
    }

    private static (string? Rejection, string? Head) ParseHead(byte[] buffer, int length)
    {
        ReadOnlySpan<byte> data = buffer.AsSpan(0, length);
        int lineEnd = data.IndexOf("\r\n"u8);
        if (lineEnd < 0)
        {
            return length > MaxRequestLineBytes + 1 ? ("request-line-too-long", null) : (null, null);
        }

        if (lineEnd > MaxRequestLineBytes)
        {
            return ("request-line-too-long", null);
        }

        int headerStart = lineEnd + 2;
        int end = data[lineEnd..].IndexOf("\r\n\r\n"u8);
        if (end < 0)
        {
            return length - headerStart >= MaxHeaderBytes ? ("headers-too-large", null) : (null, null);
        }

        end += lineEnd;
        return end + 4 - headerStart > MaxHeaderBytes ? ("headers-too-large", null) : (null, Encoding.Latin1.GetString(buffer, 0, end));
    }

    private static string Unescape(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

    private void Begin()
    {
        _lifetimeTimer = _time.CreateTimer(static state => ((LoopbackListener)state!).Expire(), this, Lifetime, Timeout.InfiniteTimeSpan);
        _acceptLoop = Task.Run(AcceptLoopAsync);
        DesktopLogMessages.LoopbackStarted(_logger);
    }

    private void Expire()
    {
        if (TryFinish(LoopbackResult.FromExpiry()))
        {
            DesktopLogMessages.LoopbackExpired(_logger);
        }
    }

    private bool TryFinish(LoopbackResult result)
    {
        if (Interlocked.CompareExchange(ref _finished, 1, 0) != 0)
        {
            return false;
        }

        _lifetimeTimer?.Dispose();
        try
        {
            _acceptStop.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Disposed at the same moment: nothing left to stop.
        }

        _listener.Stop();
        _ = _result.TrySetResult(result);
        return true;
    }

    private async Task AcceptLoopAsync()
    {
        CancellationToken token = _acceptStop.Token;
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            _ = HandleSafeAsync(client);
        }
    }

    private async Task HandleSafeAsync(TcpClient client)
    {
        try
        {
            using (client)
            {
                if (Interlocked.Increment(ref _connections) <= MaxConcurrentConnections)
                {
                    await HandleAsync(client).ConfigureAwait(false);
                }
            }
        }
        catch (IOException)
        {
            // The peer went away: nothing to answer.
        }
        catch (SocketException)
        {
            // The peer went away: nothing to answer.
        }
        catch (OperationCanceledException)
        {
            // The read timed out or the listener was disposed: the connection simply closes.
        }
        catch (ObjectDisposedException)
        {
            // The listener was disposed while the request was handled.
        }
        finally
        {
            _ = Interlocked.Decrement(ref _connections);
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        using var timeout = new CancellationTokenSource(ReadTimeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, _stop.Token);
        CancellationToken token = linked.Token;

        (string? rejection, string? head) = await ReadHeadAsync(stream, token).ConfigureAwait(false);
        if (rejection is null && head is null)
        {
            return;
        }

        int status;
        string page;
        if (rejection is not null)
        {
            (status, page) = Reject(string.Equals(rejection, "request-line-too-long", StringComparison.Ordinal) ? 414 : 431, rejection);
        }
        else
        {
            (status, page) = Process(head!);
        }

        await WriteResponseAsync(stream, status, page, token).ConfigureAwait(false);
        await DrainAsync(client, stream, token).ConfigureAwait(false);
    }

    private static async Task<(string? Rejection, string? Head)> ReadHeadAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[MaxRequestLineBytes + 2 + MaxHeaderBytes + 4];
        int length = 0;
        while (length < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return (null, null);
            }

            length += read;
            (string? rejection, string? head) = ParseHead(buffer, length);
            if (rejection is not null || head is not null)
            {
                return (rejection, head);
            }
        }

        return ("headers-too-large", null);
    }

    private (int Status, string Page) Reject(int status, string reason)
    {
        _ = Interlocked.Increment(ref _rejected);
        DesktopLogMessages.LoopbackRequestRejected(_logger, reason);
        return (status, InvalidPage);
    }

    private (int Status, string Page) Process(string head)
    {
        string[] lines = head.Split("\r\n");
        string[] parts = lines[0].Split(' ');
        if (parts.Length != 3 || parts[2] is not ("HTTP/1.1" or "HTTP/1.0"))
        {
            return Reject(400, "malformed-request-line");
        }

        if (!string.Equals(parts[0], "GET", StringComparison.Ordinal))
        {
            return Reject(405, "method");
        }

        string? hostFailure = CheckHost(lines);
        if (hostFailure is not null)
        {
            return Reject(400, hostFailure);
        }

        string target = parts[1];
        if (!target.StartsWith(CallbackPrefix, StringComparison.Ordinal))
        {
            return Reject(404, "path");
        }

        int queryStart = target.IndexOf('?', StringComparison.Ordinal);
        string path = queryStart < 0 ? target : target[..queryStart];
        string query = queryStart < 0 ? string.Empty : target[(queryStart + 1)..];
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(path[CallbackPrefix.Length..]), _nonce))
        {
            return Reject(404, "nonce");
        }

        return Complete(query);
    }

    private string? CheckHost(string[] lines)
    {
        int hosts = 0;
        string? host = null;
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0 || line[..colon].AsSpan().ContainsAny(" \t"))
            {
                return "malformed-header";
            }

            if (string.Equals(line[..colon], "Host", StringComparison.OrdinalIgnoreCase))
            {
                hosts++;
                host = line[(colon + 1)..].Trim(' ', '\t');
            }
        }

        return hosts == 1 && string.Equals(host, _hostHeader, StringComparison.Ordinal) ? null : "host";
    }

    private (int Status, string Page) Complete(string query)
    {
        string? code = null;
        bool failed = false;
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            string key = Unescape(equals < 0 ? pair : pair[..equals]);
            if (key is "error" or "error_code")
            {
                failed = true;
            }
            else if (string.Equals(key, "code", StringComparison.Ordinal) && code is null && equals >= 0)
            {
                code = Unescape(pair[(equals + 1)..]);
            }
        }

        if (failed)
        {
            if (TryFinish(LoopbackResult.FromLinkError()))
            {
                DesktopLogMessages.LoopbackCallbackError(_logger);
                return (400, InvalidPage);
            }

            return Reject(400, "callback-already-used");
        }

        if (string.IsNullOrEmpty(code) || code.Length > MaxCodeLength)
        {
            return Reject(400, "no-code");
        }

        if (!TryFinish(LoopbackResult.FromCode(new SecretString(code))))
        {
            return Reject(400, "callback-already-used");
        }

        DesktopLogMessages.LoopbackCallbackAccepted(_logger);
        return (200, ClosePage);
    }
}
