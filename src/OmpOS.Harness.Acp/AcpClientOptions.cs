namespace OmpOS.Harness.Acp;

/// <summary>Options for starting an ACP session against <c>omp acp</c>.</summary>
public sealed record AcpClientOptions
{
    /// <summary>Gets the path to the omp executable. <see langword="null"/> resolves <c>omp</c> from <c>PATH</c>.</summary>
    public string? OmpPath { get; init; }

    /// <summary>Gets the absolute working directory the session runs in. Defaults to the current directory.</summary>
    public string WorkingDirectory { get; init; } = Environment.CurrentDirectory;

    /// <summary>Gets how long the <c>initialize</c> round-trip may take before the handshake fails. Defaults to 10 seconds.</summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets how long any non-prompt request may take. Prompts are not bounded by this value. Defaults to 60 seconds.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets the policy that answers agent permission requests. Defaults to <see cref="AcpApprovalPolicy.DenyAll"/>.</summary>
    public AcpApprovalPolicy ApprovalPolicy { get; init; } = AcpApprovalPolicy.DenyAll;

    /// <summary>
    /// Gets the ACP authentication method id sent in <c>authenticate</c> when the agent advertises
    /// <c>authMethods</c> in its <c>initialize</c> result. <see langword="null"/> (the default) means no
    /// method is configured: a handshake that requires authentication then fails instead of guessing a method.
    /// </summary>
    public string? AuthMethodId { get; init; }

    /// <summary>
    /// Gets the absolute directory the agent may read and write through <c>fs/read_text_file</c> and
    /// <c>fs/write_text_file</c>. <see langword="null"/> (the default) disables both capabilities: <c>initialize</c>
    /// advertises them as <see langword="false"/> and agent calls are answered with a JSON-RPC error.
    /// </summary>
    public string? FsRoot { get; init; }
}
