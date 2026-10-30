namespace Autonogrammer.Api.DataPlane;

/// <summary>Receives the process exit code the schema wait asks for, so a test host does not set the exit code of the test process.</summary>
internal interface IExitCodeSink
{
    /// <summary>Records the exit code.</summary>
    /// <param name="exitCode">The exit code.</param>
    void Set(int exitCode);
}
