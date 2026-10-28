namespace OmpOS.Presentation.Mcp;

/// <summary>What an <see cref="IMcpSettingsSource"/> knows at one moment.</summary>
/// <param name="Servers">Every effective server, in precedence order.</param>
/// <param name="Files">Every configuration file that exists.</param>
/// <param name="Notices">Every configuration problem.</param>
/// <param name="OwnedConfigOrigin">The file the add-from-template action writes, to show; <see langword="null"/> when no workspace is open.</param>
/// <param name="CanCheck">Whether this host can run a connection check.</param>
public sealed record McpSettingsSnapshot(
    IReadOnlyList<McpServerEntry> Servers,
    IReadOnlyList<McpFileEntry> Files,
    IReadOnlyList<McpNotice> Notices,
    string? OwnedConfigOrigin,
    bool CanCheck)
{
    /// <summary>Gets the snapshot before the first load.</summary>
    public static McpSettingsSnapshot Empty { get; } = new([], [], [], null, CanCheck: false);
}
