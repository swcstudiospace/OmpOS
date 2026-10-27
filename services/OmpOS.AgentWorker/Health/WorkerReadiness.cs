using Microsoft.Extensions.Options;
using OmpOS.AgentWorker.Configuration;

namespace OmpOS.AgentWorker.Health;

/// <summary>
/// Decides whether the worker can serve a session: the workspace root exists (or can be created) and the omp executable is present.
/// The check reads the local file system only. It makes no network call and starts no process, because any outbound packet keeps a
/// Railway Serverless service awake.
/// </summary>
internal sealed class WorkerReadiness
{
    private readonly WorkerOptions _options;

    /// <summary>Initializes a new instance of the <see cref="WorkerReadiness"/> class.</summary>
    /// <param name="options">The validated worker options.</param>
    public WorkerReadiness(IOptions<WorkerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <summary>Checks readiness.</summary>
    /// <returns>An empty string when the worker is ready, otherwise a fixed reason that contains no configured value.</returns>
    public string Check()
    {
        try
        {
            Directory.CreateDirectory(_options.WorkspaceRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "workspace root is not usable";
        }

        return OmpExecutableExists() ? string.Empty : "omp executable not found";
    }

    private bool OmpExecutableExists()
    {
        var configured = _options.Omp.Path;
        if (configured.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return File.Exists(configured);
        }

        var searchPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return searchPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory, configured)));
    }
}
