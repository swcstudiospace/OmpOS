using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Hosting;

/// <summary>
/// Builds <see cref="RequestTimeoutOptions"/>: a default policy that answers 504 with a ProblemDetails body, and the
/// <see cref="HostingPolicies.NoTimeout"/> policy for streaming routes.
/// </summary>
internal sealed class ConfigureRequestTimeoutOptions : IConfigureOptions<RequestTimeoutOptions>
{
    private static readonly TimeSpan NoTimeoutLimit = TimeSpan.FromMilliseconds(int.MaxValue);

    private readonly IOptions<HostingOptions> _hosting;

    /// <summary>Initializes a new instance of the <see cref="ConfigureRequestTimeoutOptions"/> class.</summary>
    /// <param name="hosting">The hosting options.</param>
    public ConfigureRequestTimeoutOptions(IOptions<HostingOptions> hosting)
    {
        _hosting = hosting;
    }

    /// <inheritdoc />
    public void Configure(RequestTimeoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.DefaultPolicy = new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(_hosting.Value.RequestTimeoutSeconds),
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            WriteTimeoutResponse = WriteTimeoutAsync,
        };
        options.AddPolicy(HostingPolicies.NoTimeout, new RequestTimeoutPolicy
        {
            Timeout = NoTimeoutLimit,
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            WriteTimeoutResponse = WriteTimeoutAsync,
        });
    }

    /// <summary>Writes the 504 ProblemDetails body of a timed-out request.</summary>
    /// <param name="context">The timed-out request.</param>
    /// <returns>A task representing the write.</returns>
    internal static async Task WriteTimeoutAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
        var problems = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        _ = await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "The request timed out.",
                Detail = "The server did not finish the request in time.",
            },
        }).ConfigureAwait(false);
    }
}
