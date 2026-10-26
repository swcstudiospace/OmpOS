namespace OmpOS.Harness.AgentLoop.Gsd;

/// <summary>
/// Why a query of the workflow engine's command line tool failed. The reason is a category, never raw tool output.
/// </summary>
public enum GsdToolsFailure
{
    /// <summary>The tool script could not be found at any of the searched locations.</summary>
    ToolsNotFound,

    /// <summary>The workspace has no <c>.planning</c> directory, so there is no workflow state to read.</summary>
    WorkspaceNotInitialized,

    /// <summary>The planning files could not be inspected, for example because of a permission error.</summary>
    Unreadable,

    /// <summary>The node executable could not be started.</summary>
    StartFailed,

    /// <summary>The tool exited with a non-zero exit code.</summary>
    NonZeroExit,

    /// <summary>The tool did not finish within the time limit and its process tree was killed.</summary>
    TimedOut,

    /// <summary>The tool wrote more output than the limit allows and its process tree was killed.</summary>
    OutputTooLarge,

    /// <summary>The tool's output was not the expected JSON document.</summary>
    InvalidOutput,
}
