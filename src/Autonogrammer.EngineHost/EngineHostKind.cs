namespace Autonogrammer.EngineHost;

/// <summary>
/// Where the engine behind an <see cref="IEngineHost"/> runs.
/// </summary>
public enum EngineHostKind
{
    /// <summary>The harness runs in this process (desktop): in-memory adapter or a local omp child process.</summary>
    InProcess,

    /// <summary>The host consumes the backend API only (mobile): prompts and SSE streams over HTTP.</summary>
    Remote,
}
