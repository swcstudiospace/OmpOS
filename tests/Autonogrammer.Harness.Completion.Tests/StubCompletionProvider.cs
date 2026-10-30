using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Autonogrammer.Harness.Completion.Tests;

/// <summary>
/// A local HTTP endpoint that answers every request with a fixed body and status, and records each request it receives.
/// It serves on one background thread, so disposing it stops the listener and waits for that thread to finish.
/// </summary>
internal sealed class StubCompletionProvider : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly string _responseBody;
    private readonly HttpStatusCode _status;
    private readonly List<StubRequest> _requests = [];
    private readonly Thread _serving;

    private StubCompletionProvider(HttpListener listener, Uri endpoint, string responseBody, HttpStatusCode status)
    {
        _listener = listener;
        Endpoint = endpoint;
        _responseBody = responseBody;
        _status = status;
        _serving = new Thread(Serve) { IsBackground = true };
        _serving.Start();
    }

    /// <summary>Gets the URI the stub listens on.</summary>
    public Uri Endpoint { get; }

    /// <summary>Gets the number of requests received so far.</summary>
    public int RequestCount
    {
        get
        {
            lock (_requests)
            {
                return _requests.Count;
            }
        }
    }

    /// <summary>Gets the requests received so far, in arrival order.</summary>
    public IReadOnlyList<StubRequest> Requests
    {
        get
        {
            lock (_requests)
            {
                return _requests.ToArray();
            }
        }
    }

    /// <summary>Starts a stub that answers every request with the given body and status.</summary>
    /// <param name="responseBody">The JSON body to answer with.</param>
    /// <param name="status">The status code to answer with.</param>
    /// <returns>The running stub.</returns>
    public static StubCompletionProvider Start(string responseBody, HttpStatusCode status = HttpStatusCode.OK)
    {
        var port = FreeLoopbackPort();
        var endpoint = new Uri($"http://127.0.0.1:{port}/complete");

        var listener = new HttpListener();
        listener.Prefixes.Add($"{endpoint.AbsoluteUri}/");
        listener.Start();

        return new StubCompletionProvider(listener, endpoint, responseBody, status);
    }

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

            Answer(context);
        }
    }

    private void Answer(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = reader.ReadToEnd();

        lock (_requests)
        {
            _requests.Add(new StubRequest(context.Request.HttpMethod, context.Request.ContentType, body));
        }

        var bytes = Encoding.UTF8.GetBytes(_responseBody);
        context.Response.StatusCode = (int)_status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes);
        context.Response.Close();
    }
}
