using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;

namespace OmpOS.Api.Health;

/// <summary>
/// Harness adapter readiness for <c>/health/ready</c>. The in-memory adapter needs no external process and is always ready.
/// The ACP adapter is ready when <c>Harness:Acp:OmpPath</c> resolves to an existing file: an absolute path that exists, or a
/// bare name that a PATH directory holds. The probe checks the file only; it never starts omp.
/// </summary>
internal sealed class HarnessReadinessCheck : IHealthCheck
{
    private readonly IOptions<HarnessOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="HarnessReadinessCheck"/> class.</summary>
    /// <param name="options">The harness options: the adapter name and the ACP omp path.</param>
    public HarnessReadinessCheck(IOptions<HarnessOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var harness = _options.Value;
        if (HarnessOptions.IsInMemoryAdapter(harness.Adapter))
        {
            return Task.FromResult(HealthCheckResult.Healthy("In-memory harness adapter is ready."));
        }

        if (HarnessOptions.IsAcpAdapter(harness.Adapter))
        {
            return Task.FromResult(CheckOmpPath(harness.Acp.OmpPath));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy($"Harness:Adapter '{harness.Adapter}' is not a supported adapter (InMemory or Acp)."));
    }

    private static HealthCheckResult CheckOmpPath(string ompPath)
    {
        if (string.IsNullOrWhiteSpace(ompPath))
        {
            return HealthCheckResult.Unhealthy("Harness:Acp:OmpPath is not set; the Acp adapter needs the omp executable path.");
        }

        if (Path.IsPathRooted(ompPath))
        {
            return File.Exists(ompPath)
                ? HealthCheckResult.Healthy($"Harness:Acp:OmpPath resolves to {ompPath}.")
                : HealthCheckResult.Unhealthy($"Harness:Acp:OmpPath names no existing file; tried {ompPath}.");
        }

        if (ompPath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            return HealthCheckResult.Unhealthy($"Harness:Acp:OmpPath must be an absolute path or a bare executable name; relative path '{ompPath}' was not probed.");
        }

        var tried = new List<string>();
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, ompPath);
            if (File.Exists(candidate))
            {
                return HealthCheckResult.Healthy($"Harness:Acp:OmpPath resolves to {candidate}.");
            }

            tried.Add(candidate);
        }

        return HealthCheckResult.Unhealthy(tried.Count == 0
            ? $"Harness:Acp:OmpPath '{ompPath}' was not found: PATH has no directories to search."
            : $"Harness:Acp:OmpPath '{ompPath}' was not found on PATH; tried {string.Join(", ", tried)}.");
    }
}
