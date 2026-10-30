using System.Globalization;
using System.Windows.Input;

namespace Autonogrammer.Presentation.Mcp;

/// <summary>The servers of one configuration file, so the page lists servers by scope and file.</summary>
public sealed class McpFileGroupModel
{
    /// <summary>Creates a group.</summary>
    /// <param name="file">The file.</param>
    /// <param name="rows">The file's effective servers.</param>
    /// <param name="open">Opens the file.</param>
    public McpFileGroupModel(McpFileEntry file, IReadOnlyList<McpServerRowModel> rows, ICommand open)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(open);
        File = file;
        Rows = rows;
        OpenCommand = open;
        Heading = (file.Scope == McpEntryScope.Project ? "Project: " : "User: ") + file.Origin;
        CountText = file.ServerCount == 1 ? "1 server" : file.ServerCount.ToString(CultureInfo.InvariantCulture) + " servers";
        ProblemText = file.Problem ?? string.Empty;
        HasProblem = file.Problem is not null;
        OpenName = "Open file " + file.Origin;
    }

    /// <summary>Gets the file.</summary>
    public McpFileEntry File { get; }

    /// <summary>Gets the file's effective servers, in file order. A server that another file shadows is not listed.</summary>
    public IReadOnlyList<McpServerRowModel> Rows { get; }

    /// <summary>Gets the heading, for example <c>Project: .cursor/mcp.json</c>.</summary>
    public string Heading { get; }

    /// <summary>Gets how many servers the file declares, for example <c>2 servers</c>.</summary>
    public string CountText { get; }

    /// <summary>Gets whether the whole file could not be used.</summary>
    public bool HasProblem { get; }

    /// <summary>Gets the problem sentence, or an empty string.</summary>
    public string ProblemText { get; }

    /// <summary>Gets the accessible name of the Open file button.</summary>
    public string OpenName { get; }

    /// <summary>Gets the command of the Open file button.</summary>
    public ICommand OpenCommand { get; }
}
