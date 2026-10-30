namespace Autonogrammer.AgentWorker.Configuration;

/// <summary>Binds the <c>Worker:Omp</c> configuration section: how the worker starts <c>omp acp</c> for a session.</summary>
internal sealed class WorkerOmpOptions
{
    /// <summary>Gets or sets the omp executable. A bare name is resolved from <c>PATH</c>. Defaults to <c>omp</c>.</summary>
    public string Path { get; set; } = "omp";

    /// <summary>Gets or sets the arguments appended after <c>acp</c>, one argv element each. Never put a secret here: argv is visible to other processes.</summary>
    public List<string> ExtraArguments { get; set; } = [];
}
