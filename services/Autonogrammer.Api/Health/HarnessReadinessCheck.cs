using Autonogrammer.Api.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Health;

/// <summary>
/// Harness adapter readiness for <c>/health/ready</c>. The in-memory adapter needs no external process and is always ready.
/// The ACP and RPC adapters are ready when their omp path (<c>Harness:Acp:OmpPath</c> or <c>Harness:Rpc:OmpPath</c>) resolves
/// to an existing file: an absolute path that exists, or a bare name that a PATH directory holds. The probe checks the file
/// only; it never starts omp.
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
            return Task.FromResult(CheckOmpPath(harness.Acp.OmpPath, "Harness:Acp:OmpPath", HarnessOptions.AcpAdapter));
        }

        if (HarnessOptions.IsRpcAdapter(harness.Adapter))
        {
            return Task.FromResult(CheckOmpPath(harness.Rpc.OmpPath, "Harness:Rpc:OmpPath", HarnessOptions.RpcAdapter));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy($"Harness:Adapter '{harness.Adapter}' is not a supported adapter (InMemory, Acp or Rpc)."));
    }

    private static HealthCheckResult CheckOmpPath(string ompPath, string keyName, string adapterName)
    {
        if (string.IsNullOrWhiteSpace(ompPath))
        {
            return HealthCheckResult.Unhealthy($"{keyName} is not set; the {adapterName} adapter needs the omp executable path.");
        }

        if (Path.IsPathRooted(ompPath))
        {
            return File.Exists(ompPath)
                ? HealthCheckResult.Healthy($"{keyName} resolves to {ompPath}.")
                : HealthCheckResult.Unhealthy($"{keyName} names no existing file; tried {ompPath}.");
        }

        if (ompPath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            return HealthCheckResult.Unhealthy($"{keyName} must be an absolute path or a bare executable name; relative path '{ompPath}' was not probed.");
        }

        var tried = new List<string>();
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, ompPath);
            if (File.Exists(candidate))
            {
                return HealthCheckResult.Healthy($"{keyName} resolves to {candidate}.");
            }

            tried.Add(candidate);
        }

        return HealthCheckResult.Unhealthy(tried.Count == 0
            ? $"{keyName} '{ompPath}' was not found: PATH has no directories to search."
            : $"{keyName} '{ompPath}' was not found on PATH; tried {string.Join(", ", tried)}.");
    }
}
