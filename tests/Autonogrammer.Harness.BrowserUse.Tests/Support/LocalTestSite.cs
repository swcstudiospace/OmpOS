using System.Net;
using System.Text;

namespace Autonogrammer.Harness.BrowserUse.Tests.Support;

/// <summary>Two loopback HTTP listeners with per-port request logs and a redirect endpoint.</summary>
internal sealed class LocalTestSite : IAsyncDisposable
{
    private readonly HttpListener _granted = new();
    private readonly HttpListener _ungranted = new();
    private readonly List<string> _grantedLog = [];
    private readonly List<string> _ungrantedLog = [];
    private readonly CancellationTokenSource _stop = new();

    private LocalTestSite(int grantedPort, int ungrantedPort)
    {
        GrantedPort = grantedPort;
        UngrantedPort = ungrantedPort;
        _granted.Prefixes.Add("http://127.0.0.1:" + grantedPort.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/");
        _ungranted.Prefixes.Add("http://127.0.0.1:" + ungrantedPort.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/");
    }

    /// <summary>Gets the port treated as granted.</summary>
    public int GrantedPort { get; }

    /// <summary>Gets the port treated as ungranted.</summary>
    public int UngrantedPort { get; }

    /// <summary>Gets the granted-port request log.</summary>
    public IReadOnlyList<string> GrantedLog
    {
        get
        {
            lock (_grantedLog)
            {
                return _grantedLog.ToArray();
            }
        }
    }

    /// <summary>Gets the ungranted-port request log.</summary>
    public IReadOnlyList<string> UngrantedLog
    {
        get
        {
            lock (_ungrantedLog)
            {
                return _ungrantedLog.ToArray();
            }
        }
    }

    /// <summary>Starts both listeners.</summary>
    /// <returns>The site.</returns>
    public static LocalTestSite Start()
    {
        var site = new LocalTestSite(FreePort(), FreePort());
        site._granted.Start();
        site._ungranted.Start();
        _ = ServeAsync(site._granted, site._grantedLog, site._stop.Token);
        _ = ServeAsync(site._ungranted, site._ungrantedLog, site._stop.Token);
        return site;
    }

    /// <summary>Builds a redirect URL on the granted port.</summary>
    /// <param name="location">The redirect target.</param>
    /// <returns>The URL.</returns>
    public Uri RedirectUrl(string location) =>
        new("http://127.0.0.1:" + GrantedPort.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/redirect?to=" + Uri.EscapeDataString(location));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(true);
        _granted.Close();
        _ungranted.Close();
        _stop.Dispose();
    }

    private static async Task ServeAsync(HttpListener listener, List<string> log, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is OperationCanceledException or HttpListenerException or ObjectDisposedException)
            {
                break;
            }

            lock (log)
            {
                log.Add(context.Request.Url?.AbsoluteUri ?? context.Request.RawUrl ?? string.Empty);
            }

            var target = context.Request.QueryString["to"];
            if (string.Equals(context.Request.Url?.AbsolutePath, "/redirect", StringComparison.Ordinal) && !string.IsNullOrEmpty(target))
            {
                context.Response.StatusCode = 302;
                context.Response.RedirectLocation = target;
            }
            else
            {
                var body = Encoding.ASCII.GetBytes("ok");
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body, cancellationToken).ConfigureAwait(true);
            }

            context.Response.Close();
        }
    }

    private static int FreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
