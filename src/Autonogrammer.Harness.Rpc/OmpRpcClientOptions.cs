namespace Autonogrammer.Harness.Rpc;

/// <summary>Options for <see cref="OmpRpcClient"/>.</summary>
/// <remarks>
/// This client deliberately accepts no permission resolver: omp RPC has no observed permission-request
/// surface (OQ-525, open), so there is nothing to route an approval decision to and no code path in this
/// transport may grant one. RPC sessions therefore stay deny-by-default — advertised as
/// <c>approvals=denied</c> — and approval-configured flows remain on ACP until a permission frame is
/// observed. Adding an approval knob here before that observation would be inventing protocol.
/// </remarks>
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

    /// <summary>
    /// Gets the environment the engine child runs with. <see langword="null"/> (the default) inherits the
    /// parent environment unchanged. A non-null dictionary is the child's complete environment: inherited
    /// variables are cleared first, then each entry is set, so an empty dictionary means the child receives
    /// no inherited variables at all (never "inherit"), and an entry with an empty value deliberately
    /// clears that inherited variable instead of setting it. Callers pass already-resolved isolation
    /// output (for example from the ACP <c>EngineEnvironmentPolicy</c> at a composition root); this
    /// transport does not resolve allowlists or learn configuration.
    /// </summary>
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }
}
