using Autonogrammer.Sessions.Postgres;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Answers a write that the erasure admission refused (<see cref="AccountErasingException"/>) with a 409 problem at <c>/problems/account-unavailable</c>. The
/// problem carries a fixed sentence: it names neither the account nor any SQL. It is a middleware, like <see cref="DataPlaneProblemMiddleware"/>, so the
/// mapping holds in every environment (the exception handler runs outside Development only). It does nothing once the response has started: the exception
/// then reaches the caller unchanged.
/// </summary>
internal sealed class AccountUnavailableProblemMiddleware
{
    /// <summary>Gets the problem type.</summary>
    public const string ProblemType = "/problems/account-unavailable";

    private readonly RequestDelegate _next;
    private readonly IProblemDetailsService _problems;

    /// <summary>Initializes a new instance of the <see cref="AccountUnavailableProblemMiddleware"/> class.</summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="problems">The problem details writer.</param>
    public AccountUnavailableProblemMiddleware(RequestDelegate next, IProblemDetailsService problems)
    {
        _next = next;
        _problems = problems;
    }

    /// <summary>Runs the rest of the pipeline and maps a refused account write.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when the response is written.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (AccountErasingException) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(context).ConfigureAwait(false);
        }
    }

    private async Task WriteProblemAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        _ = await _problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status409Conflict,
                Type = ProblemType,
                Title = "Account is being erased",
                Detail = "The account is unavailable for changes.",
            },
        }).ConfigureAwait(false);
    }
}
