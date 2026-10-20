namespace OmpOS.Harness.Rpc;

/// <summary>Options for <see cref="OmpRpcClient"/>.</summary>
public sealed record OmpRpcClientOptions
{
    /// <summary>Gets the working directory the engine operates in. Must exist.</summary>
    public required string Cwd { get; init; }

    /// <summary>Gets the omp executable: an absolute path, or a name resolved through PATH. Defaults to <c>omp</c>.</summary>
    public string OmpPath { get; init; } = "omp";

    /// <summary>Gets how long to wait for the engine's <c>ready</c> frame. Defaults to 30 seconds.</summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets how long to wait for a command response or a prompt's completion. Defaults to 60 seconds.</summary>
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(60);
}
