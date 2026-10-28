namespace OmpOS.Harness.Remote.Containers;

/// <summary>How OmpOS runs <c>wsl.exe</c>. WSL exists on Windows only: on another system every run fails with a "wsl.exe is not available" diagnosis.</summary>
public sealed record WslClientOptions
{
    /// <summary>Gets the wsl.exe executable: a bare name resolved from <c>PATH</c> or an absolute path. Defaults to <c>wsl.exe</c>.</summary>
    public string WslPath { get; init; } = "wsl.exe";

    /// <summary>Gets how long a short non-interactive wsl.exe command (a listing or a probe) may run. Defaults to 60 seconds. The first command may have to start the distribution.</summary>
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(60);
}
