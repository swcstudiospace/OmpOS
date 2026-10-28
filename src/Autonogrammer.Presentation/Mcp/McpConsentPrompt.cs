namespace Autonogrammer.Presentation.Mcp;

/// <summary>What the consent prompt shows for one server: the exact declaration that approval covers.</summary>
public sealed class McpConsentPrompt
{
    /// <summary>Creates the prompt.</summary>
    /// <param name="entry">The server.</param>
    public McpConsentPrompt(McpServerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Name = entry.Name;
        Summary = entry.Name + " (" + entry.Transport + " server, " + entry.Origin + ")";
        CommandText = entry.CommandPreview;
        EnvironmentText = McpCopy.NamesText("Environment", entry.EnvNames);
        HeadersText = McpCopy.NamesText("Headers", entry.HeaderNames);
        ApproveName = "Approve server " + entry.Name;
        CancelName = McpCopy.CancelName;
    }

    /// <summary>Gets the server name.</summary>
    public string Name { get; }

    /// <summary>Gets the one-line description of the server.</summary>
    public string Summary { get; }

    /// <summary>Gets the command as written, or the host of a remote server.</summary>
    public string CommandText { get; }

    /// <summary>Gets the environment variable names, labelled, or an empty string.</summary>
    public string EnvironmentText { get; }

    /// <summary>Gets the header names, labelled, or an empty string.</summary>
    public string HeadersText { get; }

    /// <summary>Gets the accessible name of the approve button.</summary>
    public string ApproveName { get; }

    /// <summary>Gets the accessible name of the cancel button.</summary>
    public string CancelName { get; }
}
