using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Stands in for the Railway GraphQL endpoint: matches each request on the operation name in its GraphQL document, records it,
/// and answers with the fixture registered for that operation (the n-th call gets the n-th fixture, the last one repeats).
/// </summary>
internal sealed partial class RailwayFixtureHandler : HttpMessageHandler
{
    private readonly Dictionary<string, List<Func<HttpResponseMessage>>> _fixtures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);

    /// <summary>Gets every request received, in order.</summary>
    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>
    /// Builds a JSON response.
    /// </summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="body">The body text.</param>
    /// <param name="retryAfter">An optional <c>Retry-After</c> delay.</param>
    /// <returns>The response; the caller disposes it.</returns>
    public static HttpResponseMessage Json(HttpStatusCode status, string body, TimeSpan? retryAfter = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (retryAfter is { } delay)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(delay);
        }

        return response;
    }

    /// <summary>
    /// Registers a 200 response with the given GraphQL <c>data</c> object for an operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="dataJson">The JSON of the <c>data</c> member.</param>
    public void Data(string operation, string dataJson) => Add(operation, () => Json(HttpStatusCode.OK, "{\"data\":" + dataJson + "}"));

    /// <summary>
    /// Registers a raw response for an operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="respond">Creates the response.</param>
    public void Add(string operation, Func<HttpResponseMessage> respond)
    {
        if (!_fixtures.TryGetValue(operation, out var list))
        {
            list = [];
            _fixtures[operation] = list;
        }

        list.Add(respond);
    }

    /// <summary>
    /// Counts the requests received for an operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <returns>The number of requests.</returns>
    public int CountOf(string operation) => Requests.Count(request => string.Equals(request.Operation, operation, StringComparison.Ordinal));

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var query = JsonNode.Parse(body)?["query"]?.GetValue<string>() ?? string.Empty;
        var match = OperationName().Match(query);
        var operation = match.Success ? match.Groups["name"].Value : "(none)";
        Requests.Add(new RecordedRequest(operation, request.RequestUri, request.Headers.Authorization?.ToString(), body));
        if (!_fixtures.TryGetValue(operation, out var list))
        {
            throw new InvalidOperationException($"No fixture registered for Railway operation '{operation}'.");
        }

        var index = _calls.GetValueOrDefault(operation);
        _calls[operation] = index + 1;
        return list[Math.Min(index, list.Count - 1)]();
    }

    [GeneratedRegex(@"^\s*(?:query|mutation)\s+(?<name>\w+)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex OperationName();

    /// <summary>One request the handler received.</summary>
    /// <param name="Operation">The GraphQL operation name.</param>
    /// <param name="Uri">The request URI.</param>
    /// <param name="Authorization">The Authorization header text.</param>
    /// <param name="Body">The request body text.</param>
    internal sealed record RecordedRequest(string Operation, Uri? Uri, string? Authorization, string Body)
    {
        /// <summary>Gets the <c>variables</c> object of the request.</summary>
        public JsonNode Variables => JsonNode.Parse(Body)!["variables"]!;

        /// <summary>Gets the GraphQL document of the request.</summary>
        public string Query => JsonNode.Parse(Body)!["query"]!.GetValue<string>();
    }
}
