using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Autonogrammer.Sandbox.Abstractions;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Sandbox;

/// <summary>
/// Sends one Railway GraphQL document per call and maps every failure to <see cref="CloudEnvironmentException"/>.
/// It never retries: a mutation that received a response ran, and a retry could duplicate it.
/// </summary>
internal sealed class RailwayGraphQlTransport
{
    private static readonly Action<ILogger, string, CloudEnvironmentFailure, int, Exception?> OperationFailed =
        LoggerMessage.Define<string, CloudEnvironmentFailure, int>(
            LogLevel.Warning,
            new EventId(1, "RailwayOperationFailed"),
            "Railway operation {Operation} failed as {Failure} (HTTP {Status})");

    private readonly HttpClient _client;
    private readonly Uri _endpoint;
    private readonly string _token;
    private readonly TimeProvider _timeProvider;
    private readonly SecretRedactor _redactor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RailwayGraphQlTransport"/> class.
    /// </summary>
    /// <param name="client">The HTTP client; owned by the caller.</param>
    /// <param name="endpoint">The GraphQL endpoint.</param>
    /// <param name="token">The bearer token; already registered with <paramref name="redactor"/>.</param>
    /// <param name="timeProvider">The clock used to read an HTTP-date <c>Retry-After</c>.</param>
    /// <param name="redactor">Removes secrets from every message.</param>
    /// <param name="logger">The logger.</param>
    public RailwayGraphQlTransport(
        HttpClient client,
        Uri endpoint,
        string token,
        TimeProvider timeProvider,
        SecretRedactor redactor,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _endpoint = endpoint;
        _token = token;
        _timeProvider = timeProvider;
        _redactor = redactor;
        _logger = logger;
    }

    /// <summary>
    /// Sends a GraphQL document and returns its data.
    /// </summary>
    /// <typeparam name="TVariables">The variables type.</typeparam>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <param name="operation">The operation name, for messages and logs.</param>
    /// <param name="query">The GraphQL document.</param>
    /// <param name="variables">The variables.</param>
    /// <param name="requestInfo">Source-generated request metadata.</param>
    /// <param name="responseInfo">Source-generated response metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The data of the answer.</returns>
    /// <exception cref="CloudEnvironmentException">The request failed; see <see cref="CloudEnvironmentException.Failure"/>.</exception>
    public async Task<TData> SendAsync<TVariables, TData>(
        string operation,
        string query,
        TVariables variables,
        JsonTypeInfo<RailwayGraphQlRequest<TVariables>> requestInfo,
        JsonTypeInfo<RailwayGraphQlResponse<TData>> responseInfo,
        CancellationToken cancellationToken)
        where TData : class
    {
        var json = JsonSerializer.Serialize(new RailwayGraphQlRequest<TVariables>(query, variables), requestInfo);
        using var request = BuildRequest(json);
        int status;
        string body;
        TimeSpan? retryAfter;
        try
        {
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            status = (int)response.StatusCode;
            retryAfter = ReadRetryAfter(response.Headers.RetryAfter);
            body = await BoundedContent.ReadTextAsync(response.Content, BoundedContent.MaxVendorBodyBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw Fail(operation, CloudEnvironmentFailure.Transport, 0, "the request timed out", null, null, ex);
        }
        catch (HttpRequestException ex)
        {
            throw Fail(operation, CloudEnvironmentFailure.Transport, 0, "the request failed: " + ex.Message, null, null, ex);
        }
        catch (InvalidDataException ex)
        {
            throw Fail(operation, CloudEnvironmentFailure.InvalidResponse, 0, "the response body is over the size limit", null, null, ex);
        }

        return Interpret(operation, status, body, retryAfter, responseInfo);
    }

    private HttpRequestMessage BuildRequest(string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }

    private void ThrowForStatus(string operation, int status, TimeSpan? retryAfter, bool isBadRequest)
    {
        if (status == 429)
        {
            throw Fail(operation, CloudEnvironmentFailure.RateLimited, status, "the request was rate limited", retryAfter, null, null);
        }

        if (status is 401 or 403)
        {
            throw Fail(operation, CloudEnvironmentFailure.Unauthorized, status, "the credentials were rejected", null, null, null);
        }

        if (status >= 500)
        {
            throw Fail(operation, CloudEnvironmentFailure.Transport, status, "the server answered with an error", null, null, null);
        }

        if (!isBadRequest && status is < 200 or >= 300)
        {
            throw Fail(operation, CloudEnvironmentFailure.Transport, status, "the server answered with an unexpected status", null, null, null);
        }
    }

    private TData Interpret<TData>(
        string operation,
        int status,
        string body,
        TimeSpan? retryAfter,
        JsonTypeInfo<RailwayGraphQlResponse<TData>> responseInfo)
        where TData : class
    {
        var isBadRequest = status == 400;
        ThrowForStatus(operation, status, retryAfter, isBadRequest);

        RailwayGraphQlResponse<TData>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(body, responseInfo);
        }
        catch (JsonException ex)
        {
            throw Fail(
                operation,
                isBadRequest ? CloudEnvironmentFailure.GraphQlError : CloudEnvironmentFailure.InvalidResponse,
                status,
                "the response is not valid JSON",
                null,
                null,
                ex);
        }

        if (parsed?.Errors is { Count: > 0 } errors)
        {
            var first = errors[0];
            var message = first.Message ?? "no message";
            var traceId = first.TraceId ?? first.Extensions?.TraceId;
            if (string.Equals(message, "Not Authorized", StringComparison.OrdinalIgnoreCase))
            {
                throw Fail(operation, CloudEnvironmentFailure.Unauthorized, status, "the credentials were rejected: " + message, null, traceId, null);
            }

            var code = first.Extensions?.Code;
            var detail = code is null ? message : string.Create(CultureInfo.InvariantCulture, $"{code}: {message}");
            throw Fail(operation, CloudEnvironmentFailure.GraphQlError, status, detail, null, traceId, null);
        }

        if (isBadRequest)
        {
            throw Fail(operation, CloudEnvironmentFailure.GraphQlError, status, "the request was rejected", null, null, null);
        }

        return parsed?.Data
            ?? throw Fail(operation, CloudEnvironmentFailure.InvalidResponse, status, "the response carries neither data nor errors", null, null, null);
    }

    private TimeSpan? ReadRetryAfter(RetryConditionHeaderValue? header)
    {
        if (header is null)
        {
            return null;
        }

        if (header.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (header.Date is { } date)
        {
            var wait = date - _timeProvider.GetUtcNow();
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    private CloudEnvironmentException Fail(
        string operation,
        CloudEnvironmentFailure failure,
        int status,
        string detail,
        TimeSpan? retryAfter,
        string? traceId,
        Exception? inner)
    {
        OperationFailed(_logger, operation, failure, status, null);
        var message = _redactor.Redact(
            string.Create(CultureInfo.InvariantCulture, $"Railway {operation} failed ({failure}, HTTP {status}): {detail}"));
        return new CloudEnvironmentException(message, failure, inner)
        {
            Operation = operation,
            RetryAfter = retryAfter,
            TraceId = traceId is null ? null : _redactor.Redact(traceId),
        };
    }
}
