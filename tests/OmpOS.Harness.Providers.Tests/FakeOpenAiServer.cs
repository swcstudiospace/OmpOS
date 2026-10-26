using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OmpOS.Harness.Providers.Tests;

/// <summary>
/// An in-process stand-in for a local OpenAI-compatible server. It binds a free loopback port, answers every request with a
/// fixed status and body, and records the path and whether an Authorization header arrived. It serves on one background
/// thread; disposing it stops the listener and waits for that thread. No request leaves the machine.
/// </summary>
internal sealed class FakeOpenAiServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly string _body;
    private readonly HttpStatusCode _status;
    private readonly List<string> _paths = [];
    private readonly Thread _serving;
    private bool _sawAuthorization;

    private FakeOpenAiServer(HttpListener listener, Uri baseUri, string body, HttpStatusCode status)
    {
        _listener = listener;
        BaseUri = baseUri;
        _body = body;
        _status = status;
        _serving = new Thread(Serve) { IsBackground = true };
        _serving.Start();
    }

    /// <summary>Gets the OpenAI-style base address, ending in /v1.</summary>
    public Uri BaseUri { get; }

    /// <summary>Gets the request paths received so far, in arrival order.</summary>
    public IReadOnlyList<string> Paths
    {
        get
        {
            lock (_paths)
            {
                return [.. _paths];
            }
        }
    }

    /// <summary>Gets a value indicating whether any request carried an Authorization header.</summary>
    public bool SawAuthorization
    {
        get
        {
            lock (_paths)
            {
                return _sawAuthorization;
            }
        }
    }

    /// <summary>Starts a server that answers every request with the body and status.</summary>
    /// <param name="body">The JSON or text body to answer with.</param>
    /// <param name="status">The status code to answer with.</param>
    public static FakeOpenAiServer Start(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var port = FreeLoopbackPort();
        var root = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(root);
        listener.Start();
        return new FakeOpenAiServer(listener, new Uri(root + "v1"), body, status);
    }

    /// <summary>Returns the base address of a loopback port that nothing listens on.</summary>
    public static Uri ClosedBaseUri() => new($"http://127.0.0.1:{FreeLoopbackPort()}/v1");

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();
        _serving.Join();
        return ValueTask.CompletedTask;
    }

    private static int FreeLoopbackPort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private void Serve()
    {
        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = _listener.GetContext();
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            lock (_paths)
            {
                _paths.Add(context.Request.Url?.AbsolutePath ?? string.Empty);
                _sawAuthorization |= context.Request.Headers["Authorization"] is not null;
            }

            var bytes = Encoding.UTF8.GetBytes(_body);
            context.Response.StatusCode = (int)_status;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes);
            context.Response.Close();
        }
    }
}
