using System.Net;
using System.Text;

namespace OmpOS.Native.Tests.Completion;

/// <summary>
/// A model provider stand-in at the HTTP boundary. It answers every request with one fixed response and counts the requests
/// it receives, so a test can prove that no request was sent. No network is used.
/// </summary>
internal sealed class CountingProvider : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _responseBody;
    private readonly List<string> _requestBodies = [];
    private int _requests;

    /// <summary>Creates a provider that answers every request with the given body and status.</summary>
    /// <param name="responseBody">The JSON body of each response.</param>
    /// <param name="status">The status of each response.</param>
    public CountingProvider(string responseBody, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responseBody = responseBody;
        _status = status;
        Http = new HttpClient(this, disposeHandler: false);
    }

    /// <summary>Gets the HTTP client a completion client posts through.</summary>
    public HttpClient Http { get; }

    /// <summary>Gets the number of requests received.</summary>
    public int Requests => Volatile.Read(ref _requests);

    /// <summary>Gets the body of each request received, in order.</summary>
    public IReadOnlyList<string> RequestBodies
    {
        get
        {
            lock (_requestBodies)
            {
                return _requestBodies.ToArray();
            }
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        lock (_requestBodies)
        {
            _requestBodies.Add(body);
        }

        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
        };
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Http.Dispose();
        }

        base.Dispose(disposing);
    }
}
