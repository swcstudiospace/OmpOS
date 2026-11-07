namespace Autonogrammer.Api.DataPlane;

/// <summary>Sets <see cref="Environment.ExitCode"/>, which <c>Program</c> returns after the host stopped.</summary>
internal sealed class EnvironmentExitCodeSink : IExitCodeSink
{
    /// <inheritdoc />
    public void Set(int exitCode) => Environment.ExitCode = exitCode;
}
