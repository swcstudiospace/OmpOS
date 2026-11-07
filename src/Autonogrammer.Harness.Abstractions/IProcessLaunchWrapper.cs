// Owners 75-06 and 76-04 extend these Harness.Abstractions seam files; never recreate them.
// 76-04 replaced the property list with Wrap so a wrapper can see the command it confines (D-28).
namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// Optional wrapper around a child process launch. Implementations return a new argument vector and never a shell string.
/// A null environment value in the returned launch unsets that variable.
/// </summary>
public interface IProcessLaunchWrapper
{
    /// <summary>Returns the launch that should be started in place of <paramref name="intended"/>.</summary>
    /// <param name="intended">The file name, argument vector, working directory and environment the caller would have started.</param>
    /// <returns>The launch to start. The file name may be a confinement tool whose arguments end with the intended command.</returns>
    ProcessLaunch Wrap(ProcessLaunch intended);
}
