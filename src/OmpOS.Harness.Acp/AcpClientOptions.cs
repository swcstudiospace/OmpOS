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
}
