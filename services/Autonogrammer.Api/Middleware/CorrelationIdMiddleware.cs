namespace Autonogrammer.Api.Middleware;

/// <summary>
/// Accepts an inbound <c>x-correlation-id</c> only when it is 1 to 64 characters from <c>[A-Za-z0-9_-]</c>;
/// anything else is replaced with a minted GUIDv7, so attacker-shaped ids
/// never reach the log scope, the response header, or the persisted event log.
/// The id mirrors onto <see cref="HttpContext.TraceIdentifier"/>, the response header, and
/// the log scope, so every response — including errors — carries the identifier end to end.
/// </summary>
internal sealed class CorrelationIdMiddleware
{
    /// <summary>Gets the correlation header name.</summary>
    public const string HeaderName = "x-correlation-id";

    /// <summary>Gets the maximum accepted length of an inbound correlation id.</summary>
    private const int MaxLength = 64;

    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.</summary>
    /// <param name="next">The next pipeline step.</param>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Attaches the correlation identifier and invokes the pipeline.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        var inbound = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = inbound is not null && IsValid(inbound) ? inbound : Guid.CreateVersion7().ToString("N");

        context.TraceIdentifier = correlationId;
        var captured = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = captured;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object?>(StringComparer.Ordinal) { ["CorrelationId"] = captured }))
        {
            await _next(context).ConfigureAwait(false);
        }
    }

    private static bool IsValid(string value)
    {
        if (value.Length is < 1 or > MaxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
