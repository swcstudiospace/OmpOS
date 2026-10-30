#pragma warning disable MA0048 // The plan owns this file for the options, validator, and limiter.
using System.Threading.RateLimiting;
using Autonogrammer.Api.Automations;
using Autonogrammer.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StoreMemoryOptions = Autonogrammer.Cowork.Memory.MemoryOptions;

namespace Autonogrammer.Api.Cowork.Memory;

/// <summary>
/// Options for <c>Cowork:Memory</c>. Disabled is the default. Caps match the desktop store so a host cannot raise them.
/// </summary>
internal sealed class MemoryOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cowork:Memory";

    /// <summary>The default number of memory calls one caller may make per minute.</summary>
    public const int DefaultRequestsPerMinute = 60;

    /// <summary>Gets or sets whether the memory routes are reachable. The default is false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets whether the server refuses memory reads and writes while keeping entries.</summary>
    public bool Paused { get; set; }

    /// <summary>Gets or sets whether sensitive categories may be saved when the account has not chosen.</summary>
    public bool IncludeSensitive { get; set; }

    /// <summary>Gets or sets the entry cap. The desktop value is 1000.</summary>
    public int MaxEntries { get; set; } = StoreMemoryOptions.MaxEntriesPerAccount;

    /// <summary>Gets or sets the content cap, in characters. The desktop value is 1000.</summary>
    public int MaxContent { get; set; } = StoreMemoryOptions.MaxContentChars;

    /// <summary>Gets or sets the topic cap, in characters. The desktop value is 120.</summary>
    public int MaxTopic { get; set; } = StoreMemoryOptions.MaxTopicChars;

    /// <summary>Gets or sets the hourly save cap. The desktop value is 20.</summary>
    public int MaxSavesPerHour { get; set; } = StoreMemoryOptions.MaxSavesPerSessionPerHour;

    /// <summary>Gets or sets the query cap, in characters. The desktop value is 200.</summary>
    public int MaxQuery { get; set; } = StoreMemoryOptions.MaxQueryChars;

    /// <summary>Gets or sets how many memory calls one caller may make per minute, 1 to 100000.</summary>
    public int RequestsPerMinute { get; set; } = DefaultRequestsPerMinute;
}

/// <summary>Validates <see cref="MemoryOptions"/> on start. Caps must stay at the desktop values.</summary>
internal sealed class MemoryOptionsValidator : IValidateOptions<MemoryOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, MemoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.RequestsPerMinute is < 1 or > 100_000)
        {
            failures.Add("Cowork:Memory:RequestsPerMinute must be between 1 and 100000.");
        }

        if (options.MaxEntries != StoreMemoryOptions.MaxEntriesPerAccount)
        {
            failures.Add("Cowork:Memory:MaxEntries must be 1000.");
        }

        if (options.MaxContent != StoreMemoryOptions.MaxContentChars)
        {
            failures.Add("Cowork:Memory:MaxContent must be 1000.");
        }

        if (options.MaxTopic != StoreMemoryOptions.MaxTopicChars)
        {
            failures.Add("Cowork:Memory:MaxTopic must be 120.");
        }

        if (options.MaxSavesPerHour != StoreMemoryOptions.MaxSavesPerSessionPerHour)
        {
            failures.Add("Cowork:Memory:MaxSavesPerHour must be 20.");
        }

        if (options.MaxQuery != StoreMemoryOptions.MaxQueryChars)
        {
            failures.Add("Cowork:Memory:MaxQuery must be 200.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>Token-bucket limiter of the memory routes: one bucket per caller. Rejections answer 429 with Retry-After.</summary>
internal sealed class CoworkMemoryRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>The name the memory routes require.</summary>
    public const string PolicyName = "cowork-memory";

    private readonly IOptionsMonitor<MemoryOptions> _options;
    private readonly ILogger<CoworkMemoryRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="CoworkMemoryRateLimiterPolicy"/> class.</summary>
    /// <param name="options">The memory options carrying the per-minute budget.</param>
    /// <param name="logger">The logger.</param>
    public CoworkMemoryRateLimiterPolicy(IOptionsMonitor<MemoryOptions> options, ILogger<CoworkMemoryRateLimiterPolicy> logger)
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
        return AutomationRateLimits.Bucket("cowork-memory:" + RateLimitPartitionKey.For(httpContext), _options.CurrentValue.RequestsPerMinute);
    }
}
