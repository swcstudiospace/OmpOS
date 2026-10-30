#pragma warning disable MA0048 // The plan owns this file for the options, validator, and limiter.
using System.Threading.RateLimiting;
using Autonogrammer.Api.Automations;
using Autonogrammer.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork.Projects;

/// <summary>Options for <c>Cowork:Projects</c>. Disabled is the default, so the routes answer 404 until a host turns them on.</summary>
internal sealed class ProjectsOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cowork:Projects";

    /// <summary>The default number of project calls one caller may make per minute.</summary>
    public const int DefaultRequestsPerMinute = 60;

    /// <summary>Gets or sets whether the project and global-instruction routes are reachable. The default is false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets how many project calls one caller may make per minute, 1 to 100000.</summary>
    public int RequestsPerMinute { get; set; } = DefaultRequestsPerMinute;
}

/// <summary>Validates <see cref="ProjectsOptions"/> on start. Failures name the key and never repeat a value.</summary>
internal sealed class ProjectsOptionsValidator : IValidateOptions<ProjectsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ProjectsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.RequestsPerMinute is < 1 or > 100_000)
        {
            return ValidateOptionsResult.Fail("Cowork:Projects:RequestsPerMinute must be between 1 and 100000.");
        }

        return ValidateOptionsResult.Success;
    }
}

/// <summary>Token-bucket limiter of the project routes: one bucket per caller. Rejections answer 429 with Retry-After.</summary>
internal sealed class CoworkProjectsRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>The name the project routes require.</summary>
    public const string PolicyName = "cowork-projects";

    private readonly IOptionsMonitor<ProjectsOptions> _options;
    private readonly ILogger<CoworkProjectsRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="CoworkProjectsRateLimiterPolicy"/> class.</summary>
    /// <param name="options">The project options carrying the per-minute budget.</param>
    /// <param name="logger">The logger.</param>
    public CoworkProjectsRateLimiterPolicy(IOptionsMonitor<ProjectsOptions> options, ILogger<CoworkProjectsRateLimiterPolicy> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => RateLimitRejection.Handler(_logger);

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return AutomationRateLimits.Bucket("cowork-projects:" + RateLimitPartitionKey.For(httpContext), _options.CurrentValue.RequestsPerMinute);
    }
}
