namespace OmpOS.Harness.Remote;

/// <summary>How OmpOS runs the user's <c>ssh</c> client.</summary>
public sealed record SshClientOptions
{
    /// <summary>Gets the ssh executable: a bare name resolved from <c>PATH</c> or an absolute path. Defaults to <c>ssh</c>.</summary>
    public string SshPath { get; init; } = "ssh";

    /// <summary>Gets the ssh configuration file passed with <c>-F</c>, or <see langword="null"/> for the user's default (<c>~/.ssh/config</c>).</summary>
    public string? ConfigFile { get; init; }

    /// <summary>Gets how long ssh may take to open the TCP connection (<c>ConnectTimeout</c>). Defaults to 15 seconds.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets how long a short non-interactive ssh command (a config lookup or a preflight) may run. Defaults to 60 seconds.</summary>
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the directory for ssh connection-sharing sockets, or <see langword="null"/> (the default) to leave connection sharing off.
    /// When set, sessions use <c>ControlMaster=auto</c> with <c>ControlPersist=10m</c>. The directory must exist, be private to the user and
    /// be short: a Unix socket path is limited to about 100 characters.
    /// </summary>
    public string? ControlPathDirectory { get; init; }
}
