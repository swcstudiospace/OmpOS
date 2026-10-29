namespace Autonogrammer.Harness.Acp;

/// <summary>How the <c>omp acp</c> child process, or a stdio MCP server, obtains its environment variables.</summary>
public enum EngineEnvironmentMode
{
    /// <summary>
    /// The child inherits the whole environment of the parent process, and explicit variables override inherited ones. This is the
    /// desktop behaviour: the engine runs as the user and sees what the user's shell would.
    /// </summary>
    Inherit = 0,

    /// <summary>
    /// The child's inherited environment is cleared, and only the variables <see cref="EngineEnvironmentPolicy"/> allows are kept.
    /// This is the hosted behaviour: database passwords, provider keys and token digests of the parent never reach a model-driven tool.
    /// </summary>
    Allowlist = 1,
}
