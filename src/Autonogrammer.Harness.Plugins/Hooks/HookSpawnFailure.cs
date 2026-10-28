namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Why a hook process could not be started. It lets the dispatcher tell a hook's own mistake from a broken host.
/// </summary>
public enum HookSpawnFailure
{
    /// <summary>The process started.</summary>
    None,

    /// <summary>The executable does not exist. For a shell string it is the shell itself; for <c>args</c> it is the hook's own program.</summary>
    ExecutableNotFound,

    /// <summary>The executable or the working directory cannot be accessed.</summary>
    AccessDenied,

    /// <summary>The working directory does not exist.</summary>
    WorkingDirectoryMissing,

    /// <summary>Any other start failure.</summary>
    Other,
}
