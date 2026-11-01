using System.Globalization;
using Autonogrammer.Contracts.DataPlane;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Turns a data-plane outage that reaches the pipeline into the 503 problem of <see cref="DataPlaneProblemMapper"/> with a <c>Retry-After</c> header (D-64). It is a
/// middleware, not an exception handler, because the exception handler runs outside Development only. It sits right after the correlation-id middleware, so the
/// problem carries <c>x-correlation-id</c>, and it does nothing once the response has started: the exception then reaches the caller unchanged.
/// </summary>
internal sealed partial class DataPlaneProblemMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DataPlaneProblemMapper _mapper;
    private readonly ILogger<DataPlaneProblemMiddleware> _logger;

    /// <summary>Initializes a new instance of the <see cref="DataPlaneProblemMiddleware"/> class.</summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="mapper">The mapper.</param>
    /// <param name="logger">The logger.</param>
    public DataPlaneProblemMiddleware(RequestDelegate next, DataPlaneProblemMapper mapper, ILogger<DataPlaneProblemMiddleware> logger)
    {
        _next = next;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>Runs the rest of the pipeline and maps a data-plane outage.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when the response is written.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        DataPlaneProblem? problem = null;
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex) when (!context.Response.HasStarted && _mapper.TryMap(ex, out problem))
        {
            LogOutage(_logger, problem.Component.ToString(), problem.Reason.ToString());
            context.Response.StatusCode = problem.Status;
            context.Response.Headers.RetryAfter = problem.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            await context.Response.WriteAsJsonAsync(problem, DataPlaneJsonContext.Default.DataPlaneProblem, "application/problem+json", context.RequestAborted).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 6861, Level = LogLevel.Warning, Message = "A request failed because the data-plane component {Component} is not usable ({Reason}); answered 503.")]
    private static partial void LogOutage(ILogger logger, string component, string reason);
}
