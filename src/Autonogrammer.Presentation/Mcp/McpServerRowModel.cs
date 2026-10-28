using System.Windows.Input;

namespace Autonogrammer.Presentation.Mcp;

/// <summary>
/// One server row: the labelled texts a head draws and the commands behind its buttons. Every text is built here, from names and the command as
/// written, so a head shows exactly what the row holds. The accessible name of each button names the server, so two rows never share a name.
/// </summary>
public sealed class McpServerRowModel
{
    /// <summary>Creates a row.</summary>
    /// <param name="entry">The server.</param>
    /// <param name="toggle">Switches the server on or off.</param>
    /// <param name="review">Opens the consent prompt.</param>
    /// <param name="revoke">Withdraws the approval.</param>
    /// <param name="check">Runs a connection check.</param>
    /// <param name="open">Opens the file that declares the server.</param>
    public McpServerRowModel(McpServerEntry entry, ICommand toggle, ICommand review, ICommand revoke, ICommand check, ICommand open)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(toggle);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(revoke);
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(open);

        Entry = entry;
        Name = entry.Name;
        StatusKind = entry.Status;
        StatusText = McpCopy.StatusWord(entry.Status);
        StatusDetail = McpCopy.StatusDetail(entry.Status, entry.Name, entry.Reason);
        TransportText = entry.Transport + " server, " + (entry.Scope == McpEntryScope.Project ? "project" : "user") + " file " + entry.Origin;
        ToolCountText = McpCopy.ToolCountText(entry.ToolCount);
        CommandText = entry.CommandPreview;
        EnvironmentText = McpCopy.NamesText("Environment", entry.EnvNames);
        HeadersText = McpCopy.NamesText("Headers", entry.HeaderNames);
        OAuthText = entry.HasOAuthSettings ? McpCopy.OAuthNote : string.Empty;
        IsEnabled = entry.IsEnabled;
        NeedsAttention = entry.Status is McpServerStatusKind.NeedsConsent or McpServerStatusKind.Failed or McpServerStatusKind.AuthRequired;
        ShowReview = entry.Status == McpServerStatusKind.NeedsConsent;
        ShowRevoke = entry.RequiresConsent && entry.IsConsentGranted;
        ShowCheck = entry.IsOffered;
        ToggleText = entry.IsEnabled ? McpCopy.SwitchOffLabel : McpCopy.SwitchOnLabel;
        ToggleName = (entry.IsEnabled ? "Switch off server " : "Switch on server ") + entry.Name;
        ReviewName = "Review approval for server " + entry.Name;
        RevokeName = "Withdraw approval for server " + entry.Name;
        CheckName = "Check connection of server " + entry.Name;
        OpenName = "Open file " + entry.Origin + " for server " + entry.Name;
        ToggleCommand = toggle;
        ReviewCommand = review;
        RevokeCommand = revoke;
        CheckCommand = check;
        OpenCommand = open;
    }

    /// <summary>Gets the server this row shows.</summary>
    public McpServerEntry Entry { get; }

    /// <summary>Gets the server name.</summary>
    public string Name { get; }

    /// <summary>Gets the status.</summary>
    public McpServerStatusKind StatusKind { get; }

    /// <summary>Gets the status word, for example <c>Needs approval</c>.</summary>
    public string StatusText { get; }

    /// <summary>Gets the sentence that explains the status, or an empty string.</summary>
    public string StatusDetail { get; }

    /// <summary>Gets the transport, scope and file, for example <c>stdio server, project file .cursor/mcp.json</c>.</summary>
    public string TransportText { get; }

    /// <summary>Gets how many tools the server has, or an empty string when unknown.</summary>
    public string ToolCountText { get; }

    /// <summary>Gets the command as written, or the host of a remote server.</summary>
    public string CommandText { get; }

    /// <summary>Gets the environment variable names, labelled, or an empty string.</summary>
    public string EnvironmentText { get; }

    /// <summary>Gets the header names, labelled, or an empty string.</summary>
    public string HeadersText { get; }

    /// <summary>Gets the OAuth note, or an empty string.</summary>
    public string OAuthText { get; }

    /// <summary>Gets whether the server is switched on.</summary>
    public bool IsEnabled { get; }

    /// <summary>Gets whether the row carries the attention marker: it needs approval, failed or needs sign-in.</summary>
    public bool NeedsAttention { get; }

    /// <summary>Gets whether the Review approval button shows.</summary>
    public bool ShowReview { get; }

    /// <summary>Gets whether the Withdraw approval button shows.</summary>
    public bool ShowRevoke { get; }

    /// <summary>Gets whether the Check connection button shows.</summary>
    public bool ShowCheck { get; }

    /// <summary>Gets the label of the switch button.</summary>
    public string ToggleText { get; }

    /// <summary>Gets the accessible name of the switch button.</summary>
    public string ToggleName { get; }

    /// <summary>Gets the accessible name of the Review approval button.</summary>
    public string ReviewName { get; }

    /// <summary>Gets the accessible name of the Withdraw approval button.</summary>
    public string RevokeName { get; }

    /// <summary>Gets the accessible name of the Check connection button.</summary>
    public string CheckName { get; }

    /// <summary>Gets the accessible name of the Open file button.</summary>
    public string OpenName { get; }

    /// <summary>Gets the command of the switch button.</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>Gets the command of the Review approval button.</summary>
    public ICommand ReviewCommand { get; }

    /// <summary>Gets the command of the Withdraw approval button.</summary>
    public ICommand RevokeCommand { get; }

    /// <summary>Gets the command of the Check connection button.</summary>
    public ICommand CheckCommand { get; }

    /// <summary>Gets the command of the Open file button.</summary>
    public ICommand OpenCommand { get; }
}
