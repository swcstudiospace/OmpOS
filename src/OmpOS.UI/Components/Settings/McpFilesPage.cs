using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Presentation.Settings;

namespace OmpOS.UI.Components.Settings;

/// <summary>
/// The MCP page of the web head: the MCP configuration files the workspace holds (<c>.cursor/mcp.json</c> style files in the convention folders)
/// and the problems found with them, read-only. The page lists the files; the servers they declare are started by the engine host, not by the page.
/// </summary>
public sealed class McpFilesPage : SettingsPageBase
{
    private readonly IWorkspaceSettingsReader _reader;
    private IReadOnlyList<ConventionConfigFileInfo> _files = [];
    private IReadOnlyList<ConventionDiagnosticInfo> _problems = [];

    /// <summary>Initializes a new instance of the <see cref="McpFilesPage"/> class.</summary>
    /// <param name="reader">Where the conventions are read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    public McpFilesPage(IWorkspaceSettingsReader reader)
        : base(SettingsSection.Mcp, SettingsWebCopy.McpTitle, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    /// <summary>Gets the MCP files found, empty until the page has loaded.</summary>
    public IReadOnlyList<ConventionConfigFileInfo> Files => _files;

    /// <summary>Gets the problems found with configuration files.</summary>
    public IReadOnlyList<ConventionDiagnosticInfo> Problems => _problems;

    /// <summary>Gets a value indicating whether the page loaded and found no MCP file.</summary>
    public bool IsEmpty => State == SettingsPageState.Ready && _files.Count == 0;

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(
        async token =>
        {
            var conventions = await _reader.GetConventionsAsync(token).ConfigureAwait(true);
            if (!conventions.Available)
            {
                throw new SettingsSourceException(conventions.Reason ?? SettingsWebCopy.McpUnavailable);
            }

            _files = [.. conventions.McpFiles];
            _problems = [.. conventions.Diagnostics.Where(static problem => problem.Item == ConventionItemType.Config)];
            NotifyPropertyChanged(nameof(Files));
            NotifyPropertyChanged(nameof(Problems));
            NotifyPropertyChanged(nameof(IsEmpty));
        },
        cancellationToken);
}
