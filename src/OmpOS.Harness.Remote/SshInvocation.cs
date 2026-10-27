namespace OmpOS.Harness.Remote;

/// <summary>
/// One ssh process to start: the executable, its argument list (never joined into a shell line on this machine) and the bytes to
/// write to its stdin first. The arguments never contain a secret: secrets travel in <see cref="Preamble"/> only.
/// </summary>
public sealed record SshInvocation
{
    /// <summary>Gets the ssh executable: a bare name resolved from <c>PATH</c> or an absolute path.</summary>
    public required string FileName { get; init; }

    /// <summary>Gets the arguments, one process argument each, in order.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Gets the bytes written to the process stdin before any protocol frame, or <see langword="null"/> when there are none.</summary>
    public SshStdinPreamble? Preamble { get; init; }
}
